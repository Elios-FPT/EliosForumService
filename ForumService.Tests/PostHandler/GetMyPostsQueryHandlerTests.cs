using ForumService.Core.Handler.Post.Query;
using ForumService.Core.Interfaces.Post;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Query;

namespace ForumService.Tests.PostHandler
{
    public class GetMyPostsQueryHandlerTests
    {
        // Mocks
        private readonly Mock<IPostQueryRepository> _postQueryRepoMock;

        // System Under Test
        private readonly GetMyPostsQueryHandler _handler;

        public GetMyPostsQueryHandlerTests()
        {
            _postQueryRepoMock = new Mock<IPostQueryRepository>();

            _handler = new GetMyPostsQueryHandler(_postQueryRepoMock.Object);
        }

        // Test Case 1: Happy Path - Retrieve posts successfully
        // Scenario: Repository returns a list of posts owned by the requester.
        // Expected: Returns 200, mapped DTOs, and correct pagination info.
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_ValidRequest_ReturnsListOfPosts()
        {
            // Arrange
            var requesterId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var query = new GetMyPostsQuery(RequesterId: requesterId, Page: 1, Size: 10);

            var postsFromDb = new List<Domain.Models.Post>
            {
                new Domain.Models.Post
                {
                    PostId = Guid.NewGuid(),
                    AuthorId = requesterId,
                    Title = "My First Post",
                    Content = "Content",
                    Status = "Published",
                    CategoryId = categoryId,
                    Category = new Domain.Models.Category { Name = "General" }, // Simulated Join
                    CreatedAt = DateTime.UtcNow
                },
                new Domain.Models.Post
                {
                    PostId = Guid.NewGuid(),
                    AuthorId = requesterId,
                    Title = "My Draft",
                    Status = "Draft",
                    CreatedAt = DateTime.UtcNow
                }
            };

            _postQueryRepoMock.Setup(r => r.GetMyPostsAsync(query))
                .ReturnsAsync((postsFromDb, 2));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Equal("Posts retrieved successfully.", result.Message);
            Assert.NotNull(result.ResponseData);
            Assert.Equal(2, result.ResponseData.Count());
            var firstPost = result.ResponseData.First();
            Assert.Equal("My First Post", firstPost.Title);
            Assert.Equal("General", firstPost.CategoryName);
            Assert.Equal("Published", firstPost.Status);
            Assert.Equal(requesterId, firstPost.AuthorId);

            // Check Pagination Metadata
            Assert.Equal(2, result.Pagination.TotalItems);
        }

        // Test Case 2: Empty State
        // Scenario: User has no posts.
        // Expected: Returns 200, Empty List, "No posts found" message.
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_NoPostsFound_ReturnsEmptyListWithSpecificMessage()
        {
            // Arrange
            var query = new GetMyPostsQuery(RequesterId: Guid.NewGuid());

            // Setup Repo to return empty list and 0 count
            _postQueryRepoMock.Setup(r => r.GetMyPostsAsync(query))
                .ReturnsAsync((new List<Domain.Models.Post>(), 0));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Empty(result.ResponseData);
            Assert.Equal("No posts found.", result.Message);
            Assert.Equal(0, result.Pagination.TotalItems);
        }

        // Test Case 3: Exception Handling
        // Scenario: Repository throws an exception.
        // Expected: Returns 500 Internal Server Error.
        [Fact]
        [Trait("Category", "Handler - Exception")]
        public async Task Handle_RepositoryThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var query = new GetMyPostsQuery(RequesterId: Guid.NewGuid());

            _postQueryRepoMock.Setup(r => r.GetMyPostsAsync(query))
                .ThrowsAsync(new Exception("Database connectivity issue"));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.StartsWith("An internal server error occurred", result.Message);
            Assert.Null(result.ResponseData);
        }

        // Test Case 4: Pagination Defaults
        // Scenario: Request contains invalid Page (0) and Size (-1).
        // Expected: Handler defaults to Page 1 and Size 10 in the response metadata.
        [Fact]
        [Trait("Category", "Handler - Logic")]
        public async Task Handle_InvalidPaginationInputs_DefaultsToStandardValues()
        {
            // Arrange
            var query = new GetMyPostsQuery(RequesterId: Guid.NewGuid(), Page: 0, Size: -5);

            // Setup Repo to return empty (the query itself passed to repo might still have raw values, 
            // but we check the Handler's response logic)
            _postQueryRepoMock.Setup(r => r.GetMyPostsAsync(query))
                .ReturnsAsync((new List<Domain.Models.Post>(), 0));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result.Pagination);
            Assert.Equal(1, result.Pagination.Page); // Defaulted from 0
            Assert.Equal(10, result.Pagination.PageSize); // Defaulted from -5
        }
    }
}
