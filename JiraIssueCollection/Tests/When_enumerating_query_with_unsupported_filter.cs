using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Machine.Specifications;
using TechTalk.JiraRestClient;

namespace JiraRestClient.QueryableTests
{
    [Subject("QueryableIssueCollection")]
    public class When_enumerating_query_with_unsupported_filter
    {
        public Establish context = () =>
        {
            Filter1 = i => i.id != "id1";
            Filter2 = i => i.JiraIdentifier != null;
            Filter3 = i => i.fields.assignee.name == "foo";
            JiraQueryMock = new Moq.Mock<IJiraClient<IssueFields>>();
            JiraQueryResult = new[]
            {
                new Issue<IssueFields> { id = "id2", fields = new IssueFields { assignee = new JiraUser { name = "foo" } } },
                new Issue<IssueFields> { id = "id3", fields = new IssueFields { assignee = new JiraUser { name = "bar" } } },
                new Issue<IssueFields> { id = null, fields = new IssueFields { assignee = new JiraUser { name = "baz" } } },
            };
            JiraQueryMock.Setup(m => m.EnumerateIssuesByQuery(Moq.It.IsAny<string>(), Moq.It.IsAny<int>())).Returns(JiraQueryResult);
            Subject = new QueryableIssueCollection<IssueFields>(JiraQueryMock.Object).Where(Filter1).Where(Filter2).Where(Filter3);
        };

        public Because of = () => ActionResult = Subject.ToArray();

        public It should_execute_supported_subquery = () => JiraQueryMock.Verify(m => m.EnumerateIssuesByQuery("(id!=\"id1\")", 0), Moq.Times.Once);

        public It should_return_filtered_service_result = () => ActionResult.ShouldEqualTo(JiraQueryResult.Where(Filter2.Compile()).Where(Filter3.Compile()));

        static IQueryable<Issue<IssueFields>> Subject;
        static IEnumerable<Issue<IssueFields>> ActionResult;

        static Moq.Mock<IJiraClient<IssueFields>> JiraQueryMock;
        static IEnumerable<Issue<IssueFields>> JiraQueryResult;
        static Expression<Func<Issue<IssueFields>, bool>> Filter1;
        static Expression<Func<Issue<IssueFields>, bool>> Filter2;
        static Expression<Func<Issue<IssueFields>, bool>> Filter3;
    }
}
