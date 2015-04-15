using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Machine.Specifications;
using TechTalk.JiraRestClient;

namespace JiraRestClient.QueryableTests
{
    [Subject("QueryableIssueCollection")]
    public class When_enumerating_query_with_double_filter
    {
        public Establish context = () =>
        {
            Filter1 = i => i.id != "id1";
            Filter2 = i => i.id != "id3";
            JiraQueryMock = new Moq.Mock<IJiraClient<IssueFields>>();
            JiraQueryResult = new[] { new Issue<IssueFields> { id = "id2" } };
            JiraQueryMock.Setup(m => m.EnumerateIssuesByQuery(Moq.It.IsAny<string>(), Moq.It.IsAny<int>())).Returns(JiraQueryResult);
            Subject = new QueryableIssueCollection<IssueFields>(JiraQueryMock.Object).Where(Filter1).Where(Filter2);
        };

        public Because of = () => ActionResult = Subject.ToArray();

        public It should_execute_compound_filtered_query = () => JiraQueryMock.Verify(m => m.EnumerateIssuesByQuery("(id!=\"id3\") AND (id!=\"id1\")", 0), Moq.Times.Once);

        public It should_return_service_result = () => ActionResult.ShouldEqualTo(JiraQueryResult);

        static IQueryable<Issue<IssueFields>> Subject;
        static IEnumerable<Issue<IssueFields>> ActionResult;

        static Moq.Mock<IJiraClient<IssueFields>> JiraQueryMock;
        static IEnumerable<Issue<IssueFields>> JiraQueryResult;
        static Expression<Func<Issue<IssueFields>, bool>> Filter1;
        static Expression<Func<Issue<IssueFields>, bool>> Filter2;
    }
}
