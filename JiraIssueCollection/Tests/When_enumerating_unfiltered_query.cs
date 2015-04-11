using System;
using System.Collections.Generic;
using System.Linq;
using Machine.Specifications;
using TechTalk.JiraRestClient;

namespace JiraRestClient.QueryableTests
{
    [Subject("QueryableIssueCollection")]
    public class When_enumerating_unfiltered_query
    {
        public Establish context = () =>
        {
            JiraQueryMock = new Moq.Mock<IJiraClient<IssueFields>>();
            JiraQueryResult = new[] { new Issue<IssueFields> { id = "id1" }, new Issue<IssueFields> { id = "id2" } };
            JiraQueryMock.Setup(m => m.EnumerateIssuesByQuery(Moq.It.IsAny<string>(), Moq.It.IsAny<int>())).Returns(JiraQueryResult);
            Subject = new QueryableIssueCollection<IssueFields>(JiraQueryMock.Object);
        };

        public Because of = () => ActionResult = Subject.ToArray();

        public It should_execute_empty_query = () => JiraQueryMock.Verify(m => m.EnumerateIssuesByQuery("", 0), Moq.Times.Once);

        public It should_return_service_result = () => ActionResult.ShouldEqualTo(JiraQueryResult);

        static IQueryable<Issue<IssueFields>> Subject;
        static IEnumerable<Issue<IssueFields>> ActionResult;

        static Moq.Mock<IJiraClient<IssueFields>> JiraQueryMock;
        static IEnumerable<Issue<IssueFields>> JiraQueryResult;
    }
}
