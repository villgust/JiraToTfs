using System;
using System.Linq;
using Machine.Specifications;
using TechTalk.JiraRestClient;

namespace JiraRestClient.QueryableTests
{
    [Subject("QueryableIssueCollection")]
    public class When_not_enumerating_query
    {
        public Establish context = () =>
        {
            JiraQueryMock = new Moq.Mock<IJiraClient<IssueFields>>();
            var JiraQueryResult = Strings.Select(s => new Issue<IssueFields> { id = s }).ToArray();
            JiraQueryMock.Setup(m => m.EnumerateIssuesByQuery(Moq.It.IsAny<string>(), Moq.It.IsAny<string[]>(), Moq.It.IsAny<int>())).Returns(JiraQueryResult);

            var query = new QueryableIssueCollection<IssueFields>(JiraQueryMock.Object)
                .Where(i => Strings.Contains(i.id))
                .Select(i => i.id.Length);
        };

        public Because of = () => { /* doing nothing */ };

        public It should_not_query_jira_issues = () => JiraQueryMock.Verify(m => m.EnumerateIssuesByQuery(Moq.It.IsAny<string>(), Moq.It.IsAny<string[]>(), Moq.It.IsAny<int>()), Moq.Times.Never);

        static Moq.Mock<IJiraClient<IssueFields>> JiraQueryMock;

        static string[] Strings = new[] { "x", "xx", "xxx" };
    }
}
