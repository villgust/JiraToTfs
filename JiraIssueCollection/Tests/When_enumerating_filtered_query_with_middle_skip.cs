using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Machine.Specifications;
using TechTalk.JiraRestClient;

namespace JiraRestClient.QueryableTests
{
    [Subject("QueryableIssueCollection")]
    public class When_enumerating_filtered_query_with_middle_skip
    {
        public Establish context = () =>
        {
            Filter1 = i => i.id != "id1";
            Filter2 = i => i.id != "id4";
            JiraQueryMock = new Moq.Mock<IJiraClient<IssueFields>>();
            JiraQueryResult = new[] { new Issue<IssueFields> { id = "id3" }, new Issue<IssueFields> { id = "id4" } };
            JiraQueryMock.Setup(m => m.EnumerateIssuesByQuery(Moq.It.IsAny<string>(), Moq.It.IsAny<int>())).Returns(JiraQueryResult);
            Subject = new QueryableIssueCollection<IssueFields>(JiraQueryMock.Object).Where(Filter1).Skip(1).Where(Filter2);
        };

        public Because of = () => ActionResult = Subject.ToArray();

        public It should_execute_partial_filter_with_skip = () => JiraQueryMock.Verify(m => m.EnumerateIssuesByQuery("(id!=\"id1\")", 1), Moq.Times.Once);

        public It should_return_filtered_service_result = () => ActionResult.ShouldEqualTo(JiraQueryResult.Where(Filter2.Compile()));

        static IQueryable<Issue<IssueFields>> Subject;
        static IEnumerable<Issue<IssueFields>> ActionResult;

        static Moq.Mock<IJiraClient<IssueFields>> JiraQueryMock;
        static IEnumerable<Issue<IssueFields>> JiraQueryResult;
        static Expression<Func<Issue<IssueFields>, bool>> Filter1;
        static Expression<Func<Issue<IssueFields>, bool>> Filter2;
    }
}
