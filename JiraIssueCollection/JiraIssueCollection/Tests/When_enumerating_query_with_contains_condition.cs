using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Machine.Specifications;
using TechTalk.JiraRestClient;

namespace JiraRestClient.QueryableTests
{
    [Subject("QueryableIssueCollection")]
    public class When_enumerating_query_with_contains_condition
    {
        public Establish context = () =>
        {
            var array = new[] { "id2", "id3" };
            Filter = i => array.Contains(i.id);
            JiraQueryMock = new Moq.Mock<IJiraClient<IssueFields>>();
            JiraQueryResult = new[] { new Issue<IssueFields> { id = "id2" } };
            JiraQueryMock.Setup(m => m.EnumerateIssuesByQuery(Moq.It.IsAny<string>(), Moq.It.IsAny<string[]>(), Moq.It.IsAny<int>())).Returns(JiraQueryResult);
            Subject = new QueryableIssueCollection<IssueFields>(JiraQueryMock.Object).Where(Filter);
        };

        public Because of = () => ActionResult = Subject.ToArray();

        public It should_execute_collection_query = () => JiraQueryMock.Verify(m => m.EnumerateIssuesByQuery("(id IN (\"id2\",\"id3\"))", null, 0), Moq.Times.Once);

        public It should_return_service_result = () => ActionResult.ShouldEqualTo(JiraQueryResult);

        static IQueryable<Issue<IssueFields>> Subject;
        static IEnumerable<Issue<IssueFields>> ActionResult;

        static Moq.Mock<IJiraClient<IssueFields>> JiraQueryMock;
        static IEnumerable<Issue<IssueFields>> JiraQueryResult;
        static Expression<Func<Issue<IssueFields>, bool>> Filter;
    }
}
