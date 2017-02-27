#region License
/*
    This source makes up part of JiraToTfs, a utility for migrating Jira
    tickets to Microsoft TFS.

    Copyright(C) 2016  Ian Montgomery

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/
#endregion

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TechTalk.JiraRestClient;
using TicketImporter.Interface;
using TrackProgress;

namespace TicketImporter
{
    public class JiraProject : ITicketSource
    {
        //http://atlassian.techsolcom.ca/fr/actualites/entry/153-words-that-will-break-your-jira-jql-searches
        private static string[] JQL_RESERVED = { "abort", "access", "add", "after", "alias", "all", "alter", "and", "any", "as", "asc", "audit", "avg", "before", "begin", "between", "boolean", "break", "by", "byte", "catch", "cf", "char", "character", "check", "checkpoint", "collate", "collation", "column", "commit", "connect", "continue", "count", "create", "current", "date", "decimal", "declare", "decrement", "default", "defaults", "define", "delete", "delimiter", "desc", "difference", "distinct", "divide", "do", "double", "drop", "else", "empty", "encoding", "end", "equals", "escape", "exclusive", "exec", "execute", "exists", "explain", "false", "fetch", "file", "field", "first", "float", "for", "from", "function", "go", "goto", "grant", "greater", "group", "having", "identified", "if", "immediate", "in", "increment", "index", "initial", "inner", "inout", "input", "insert", "int", "integer", "intersect", "intersection", "into", "is", "isempty", "isnull", "join", "last", "left", "less", "like", "limit", "lock", "long", "max", "min", "minus", "mode", "modify", "modulo", "more", "multiply", "next", "noaudit", "not", "notin", "nowait", "null", "number", "object", "of", "on", "option", "or", "order", "outer", "output", "power", "previous", "prior", "privileges", "public", "raise", "raw", "remainder", "rename", "resource", "return", "returns", "revoke", "right", "row", "rowid", "rownum", "rows", "select", "session", "set", "share", "size", "sqrt", "start", "strict", "string", "subtract", "sum", "synonym", "table", "then", "to", "trans", "transaction", "trigger", "true", "uid", "union", "unique", "update", "user", "validate", "values", "view", "when", "whenever", "where", "while", "with" };

        public JiraProject(string jiraServer, string jiraProject, string userName, string password)
        {
            jira = new JiraClient(jiraServer, userName, password);
            jira.OnPercentComplete += onPercentComplete;
            this.userName = userName;
            this.password = password;
            this.jiraProject = jiraProject;
            this.jiraServer = jiraServer;
            PreferHtml = false;
        }

        #region private class members

        private readonly JiraClient jira;
        private readonly string jiraServer;
        private readonly string jiraProject;
        private readonly string userName;
        private readonly string password;

        private void onPercentComplete(int percentComplete)
        {
            if (this.OnPercentComplete != null)
            {
                this.OnPercentComplete(percentComplete);
            }
        }

        private void onDetailedProcessing(string ticket)
        {
            if (OnDetailedProcessing != null)
            {
                OnDetailedProcessing(ticket);
            }
        }

        #endregion

        #region ITicketSource Interface

        public string Source
        {
            get { return "Jira"; }
        }

        public PercentComplete OnPercentComplete { set; get; }

        public DetailedProcessing OnDetailedProcessing { set; get; }

        public IEnumerable<Ticket> Tickets(IAvailableTicketTypes availableTypes)
        {
            var map = new JiraTypeMap(this, availableTypes, false);
            ConcurrentBag<Ticket> tickets = new ConcurrentBag<Ticket>();
            var issueTypes = GetListIssueTypes(map);
            Parallel.ForEach (jira.EnumerateIssues(jiraProject, issueTypes),
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                jiraKey =>
            {
                Console.WriteLine("Thread {0} comming into get ticket", Thread.CurrentThread.ManagedThreadId);

                var issueRef = new IssueRef
                {
                    id = jiraKey.id,
                    key = jiraKey.key
                };
                var jiraTicket = jira.LoadIssue(issueRef);

                onDetailedProcessing(jiraTicket.key + " - " + jiraTicket.fields.summary);

                var ticket = new Ticket();
                ticket.TicketType = map[jiraTicket.fields.issuetype.name];
                ticket.ID = jiraTicket.key;
                ticket.Summary = JiraString.StripNonPrintable(jiraTicket.fields.summary);

                var status = jiraTicket.fields.status.statusCategory.key.ToUpper();
                switch (status)
                {
                    case "NEW":
                        ticket.TicketState = Ticket.State.Todo;
                        break;
                    case "DONE":
                        ticket.TicketState = Ticket.State.Done;
                        break;
                    case "INDETERMINATE":
                        ticket.TicketState = Ticket.State.InProgress;
                        break;
                    default:
                        ticket.TicketState = Ticket.State.Unknown;
                        break;
                }

                ticket.Parent = jiraTicket.fields.parent.key;

                ticket.Description = JiraString.StripNonPrintable(jiraTicket.fields.description);
                if (PreferHtml &&
                    string.IsNullOrWhiteSpace(jiraTicket.renderedFields.description) == false)
                {
                    ticket.Description = JiraString.StripNonPrintable(jiraTicket.renderedFields.description);
                }
                ticket.CreatedOn = jiraTicket.fields.created;
                ticket.LastModified = jiraTicket.fields.updated;

                ticket.CreatedBy = new User(jiraTicket.fields.reporter.displayName,
                    jiraTicket.fields.reporter.name,
                    jiraTicket.fields.reporter.emailAddress);
                ticket.AssignedTo = new User(jiraTicket.fields.assignee.displayName,
                    jiraTicket.fields.assignee.name,
                    jiraTicket.fields.assignee.emailAddress);

                ticket.Epic = jiraTicket.fields.customfield_10800;
                ticket.ExternalReference = jiraTicket.key;
                ticket.Url = jiraServer + "/browse/" + jiraTicket.key;
                int.TryParse(jiraTicket.fields.customfield_10004, out ticket.StoryPoints);

                foreach (var link in jiraTicket.fields.issuelinks)
                {
                    if (string.Compare(link.inwardIssue.key, jiraTicket.key) != 0)
                    {
                        ticket.Links.Add(new Link(link.inwardIssue.key, link.type.name));
                    }
                    if (string.Compare(link.outwardIssue.key, jiraTicket.key) != 0)
                    {
                        ticket.Links.Add(new Link(link.outwardIssue.key, link.type.name));
                    }
                }

                foreach (var jiraComment in jiraTicket.fields.comments)
                {
                    var author = new User(jiraComment.author.displayName,
                        jiraComment.author.name, jiraComment.author.emailAddress);
                    var comment = new Comment(author, jiraComment.body, jiraComment.created);
                    if (jiraComment.updated.Date > jiraComment.created.Date)
                    {
                        comment.Updated = jiraComment.updated;
                    }
                    ticket.Comments.Add(comment);
                }

                foreach (var attachment in jiraTicket.fields.attachment)
                {
                    ticket.Attachments.Add(new Attachment(attachment.filename, attachment.content));
                }

                ticket.Priority = jiraTicket.fields.priority.name;
                ticket.Project = jiraTicket.fields.project.name;
                if (jiraTicket.fields.resolutiondate != null)
                {
                    ticket.ClosedOn = jiraTicket.fields.resolutiondate;
                }

                tickets.Add(ticket);
            });
            return tickets;
        }

        private IEnumerable<String> GetListIssueTypes(JiraTypeMap map)
        {
            foreach (var item in map.Mappings)
            {
                string issueType = item.Key;
                if (issueType.Any(Char.IsWhiteSpace) || JQL_RESERVED.Contains(issueType.ToLower()))
                    issueType = String.Format("\"{0}\"", issueType);
                yield return issueType;
            }
        }

        public void DownloadAttachments(Ticket ticket, string downloadFolder)
        {
            using (var webClient = new WebClient())
            {
                var downloaded = new Dictionary<string, int>();
                foreach (var attachment in ticket.Attachments)
                {
                    onDetailedProcessing("Downloading " + attachment.FileName);
                    var sourceUri = string.Format("{0}?&os_username={1}&os_password={2}",
                        attachment.Source, userName, password);
                    string name = Path.GetFileNameWithoutExtension(attachment.FileName),
                        extension = Path.GetExtension(attachment.FileName),
                        downloadedName = attachment.FileName;

                    // Jira can have more than one of the same file attached to a ticket ...
                    var nextCopy = 0;
                    if (downloaded.ContainsKey(attachment.FileName))
                    {
                        nextCopy = downloaded[attachment.FileName];
                        downloadedName = string.Format("{0}_{1}{2}", name, nextCopy, extension);
                    }
                    downloaded[attachment.FileName] = ++nextCopy;

                    try
                    {
                        var downloadTo = Path.Combine(downloadFolder, downloadedName);
                        webClient.DownloadFile(new Uri(sourceUri), downloadTo);
                        attachment.Source = downloadTo;
                        attachment.FileName = downloadedName;
                        attachment.Downloaded = true;
                    }
                    catch
                    {
                        attachment.Downloaded = false;
                    }
                }
            }
        }

        public List<string> GetAvailableTicketTypes()
        {
            var ticketTypes = new List<string>();
            try
            {
                if (jira != null)
                {
                    ticketTypes.AddRange(jira.GetIssueTypes().Select(type => type.name));
                }
            }
            catch
            {
                // do nothing.
            }
            return ticketTypes;
        }

        public List<string> GetAvailablePriorities()
        {
            var priorities = new List<string>();
            try
            {
                if (jira != null)
                {
                    foreach (var priority in jira.GetIssuePriorities())
                    {
                        priorities.Add(priority.name);
                    }
                }
            }
            catch
            {
                // do nothing.
            }
            return priorities;
        }

        public bool PreferHtml { get; set; }

        #endregion
    }
}