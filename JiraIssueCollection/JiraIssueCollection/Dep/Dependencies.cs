using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TechTalk.JiraRestClient
{
    public interface IJiraClient<TIssueFields> where TIssueFields : IssueFields, new()
    {
        IQueryable<Issue<TIssueFields>> QueryIssues();
        IEnumerable<Issue<TIssueFields>> EnumerateIssuesByQuery(string jqlQuery, string[] fields, int startIndex);
    }

    public class Issue<TIssueFields> : IssueRef where TIssueFields : IssueFields, new()
    {
        public Issue() { fields = new TIssueFields(); }

        public string expand { get; set; }

        public string self { get; set; }

        public TIssueFields fields { get; set; }

        internal static void ExpandLinks<T>(Issue<T> issue) where T : IssueFields, new()
        {
            foreach (var link in issue.fields.issuelinks)
            {
                if (string.IsNullOrEmpty(link.inwardIssue.id))
                {
                    link.inwardIssue.id = issue.id;
                    link.inwardIssue.key = issue.key;
                }
                if (string.IsNullOrEmpty(link.outwardIssue.id))
                {
                    link.outwardIssue.id = issue.id;
                    link.outwardIssue.key = issue.key;
                }
            }
        }
    }

    public class IssueRef
    {
        public string id { get; set; }
        public string key { get; set; }

        internal string JiraIdentifier
        {
            get { return String.IsNullOrWhiteSpace(id) ? key : id; }
        }
    }

    public class IssueFields
    {
        public IssueFields()
        {
            status = new Status();
            timetracking = new Timetracking();

            labels = new List<String>();
            comments = new List<Comment>();
            issuelinks = new List<IssueLink>();
            attachment = new List<Attachment>();
            watchers = new List<JiraUser>();
        }

        public String summary { get; set; }
        public String description { get; set; }
        public Timetracking timetracking { get; set; }
        public Status status { get; set; }

        public JiraUser reporter { get; set; }
        public JiraUser assignee { get; set; }
        public List<JiraUser> watchers { get; set; }

        public List<String> labels { get; set; }
        public List<Comment> comments { get; set; }
        public List<IssueLink> issuelinks { get; set; }
        public List<Attachment> attachment { get; set; }
    }

    public class Status
    {
        public string id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
    }

    public class Timetracking
    {
        public string originalEstimate { get; set; }
        public int originalEstimateSeconds { get; set; }

        private const decimal DayToSecFactor = 8 * 3600;
        public decimal originalEstimateDays
        {
            get
            {
                return (decimal)originalEstimateSeconds / DayToSecFactor;
            }
            set
            {
                originalEstimate = string.Format(CultureInfo.InvariantCulture, "{0}d", value);
                originalEstimateSeconds = (int)(value * DayToSecFactor);
            }
        }
    }

    public class Comment
    {
        public string id { get; set; }
        public string body { get; set; }
    }

    public class IssueLink
    {
        public IssueLink()
        {
            type = new LinkType();
            inwardIssue = new IssueRef();
            outwardIssue = new IssueRef();
        }

        public string id { get; set; }

        public LinkType type { get; set; }
        public IssueRef outwardIssue { get; set; }
        public IssueRef inwardIssue { get; set; }
    }

    public class LinkType
    {
        public string name { get; set; }
    }

    public class Attachment
    {
        public string id { get; set; }
        public string self { get; set; }
        public string filename { get; set; }
        public string content { get; set; }
    }

    public class JiraUser
    {
        public string name { get; set; }
        public string emailAddress { get; set; }
        public string displayName { get; set; }
        public bool active { get; set; }
    }
}
