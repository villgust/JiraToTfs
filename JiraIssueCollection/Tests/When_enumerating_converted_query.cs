using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Machine.Specifications;
using TechTalk.JiraRestClient;

namespace JiraRestClient.QueryableTests
{
    [Subject("QueryableIssueCollection")]
    public class When_enumerating_converted_query
    {
        public Establish context = () =>
        {
            Action = i => i.id;
            JiraQueryMock = new Moq.Mock<IJiraClient<IssueFields>>();
            JiraQueryResult = new[] { new Issue<IssueFields> { id = "id1" }, new Issue<IssueFields> { id = "id2" } };
            JiraQueryMock.Setup(m => m.EnumerateIssuesByQuery(Moq.It.IsAny<string>(), Moq.It.IsAny<int>())).Returns(JiraQueryResult);
            Subject = new QueryableIssueCollection<IssueFields>(JiraQueryMock.Object).Select(Action);
        };

        public Because of = () => ActionResult = Subject.ToArray();

        public It should_execute_empty_query = () => JiraQueryMock.Verify(m => m.EnumerateIssuesByQuery("", 0), Moq.Times.Once);

        public It should_return_converted_result = () => ActionResult.ShouldEqualTo(JiraQueryResult.Select(Action.Compile()));

        static IQueryable<string> Subject;
        static IEnumerable<string> ActionResult;

        static Moq.Mock<IJiraClient<IssueFields>> JiraQueryMock;
        static IEnumerable<Issue<IssueFields>> JiraQueryResult;
        static Expression<Func<Issue<IssueFields>, string>> Action;
    }
}
