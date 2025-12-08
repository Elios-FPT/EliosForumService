using ForumService.Contract.Models;
using ForumService.Core.Handler.Post.Query;
using ForumService.Core.Interfaces;
using ForumService.Core.Interfaces.Post;
using ForumService.Domain.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Query;

namespace ForumService.Tests.ModeratorPostHandler
{
    public class GetPendingPostsQueryHandlerTests
    {
        // Mocks
        private readonly Mock<IPostQueryRepository> _mockPostQueryRepository;
        private readonly Mock<IKafkaProducerRepository<User>> _mockProducerRepository;

        // System Under Test
        private readonly GetPendingPostsQueryHandler _handler;

        public GetPendingPostsQueryHandlerTests()
        {
            _mockPostQueryRepository = new Mock<IPostQueryRepository>();
            _mockProducerRepository = new Mock<IKafkaProducerRepository<User>>();

            _handler = new GetPendingPostsQueryHandler(
                _mockPostQueryRepository.Object,
                _mockProducerRepository.Object
            );
        }

        [Fact]
        public async Task Handle_ShouldReturnEmptyList_WhenNoPendingPostsFound()
        {
            // Arrange
            var query = new GetPendingPostsQuery();

            // Mock Repo to return empty list
            _mockPostQueryRepository.Setup(x => x.GetPendingPostsAsync(query))
                .ReturnsAsync((Enumerable.Empty<Domain.Models.Post>(), 0));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.ResponseData);
            Assert.Equal("No pending posts found.", result.Message);

            // Validate Pagination
            Assert.NotNull(result.Pagination);
            Assert.Equal(0, result.Pagination.TotalItems);

            // Verify Kafka was NOT called
            _mockProducerRepository.Verify(x => x.ProduceGetAllAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task Handle_ShouldReturnMappedPosts_WhenPostsAndUsersExist()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var query = new GetPendingPostsQuery();

            var posts = new List<Domain.Models.Post>
            {
                new Domain.Models.Post
                {
                    PostId = Guid.NewGuid(),
                    Title = "Pending Post 1",
                    AuthorId = authorId,
                    Status = "PendingReview",
                    Category = new Category { Name = "General" }
                }
            };

            // Mock Post Repo
            _mockPostQueryRepository.Setup(x => x.GetPendingPostsAsync(query))
                .ReturnsAsync((posts, 1));

            // Mock User Service (Kafka) to return profiles
            var users = new List<User>
            {
                new User { id = authorId, firstName = "Alice", lastName = "Wonder", avatarUrl = "alice.png" }
            };

            // Setup Kafka with all optional arguments explicit
            _mockProducerRepository.Setup(x => x.ProduceGetAllAsync(
                    "user",
                    "user-forum-user",
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(users);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.ResponseData);
            var dto = result.ResponseData.First();
            Assert.Equal(posts[0].Title, dto.Title);
            Assert.Equal("General", dto.CategoryName);
            Assert.Equal("PendingReview", dto.Status);
            Assert.Equal("Alice", dto.AuthorFirstName);
            Assert.Equal("Wonder", dto.AuthorLastName);
        }

        [Fact]
        public async Task Handle_ShouldReturnPostsWithoutUserDetails_WhenUserServiceFails()
        {
            // Arrange
            var query = new GetPendingPostsQuery();
            var posts = new List<Domain.Models.Post>
            {
                new Domain.Models.Post { PostId = Guid.NewGuid(), Title = "Pending Post", AuthorId = Guid.NewGuid() }
            };

            _mockPostQueryRepository.Setup(x => x.GetPendingPostsAsync(query))
                .ReturnsAsync((posts, 1));

            // Mock Kafka to throw exception
            _mockProducerRepository.Setup(x => x.ProduceGetAllAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Kafka Timeout"));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.ResponseData);
            Assert.Equal("Pending posts retrieved successfully.", result.Message); // Flow continues

            var dto = result.ResponseData.First();
            Assert.Equal("Pending Post", dto.Title);

            // User details should be null
            Assert.Null(dto.AuthorFirstName);
        }

        [Fact]
        public async Task Handle_ShouldHandlePartialUserMatches_WhenSomeUsersNotFound()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var query = new GetPendingPostsQuery();
            var posts = new List<Domain.Models.Post>
            {
                new Domain.Models.Post { PostId = Guid.NewGuid(), AuthorId = authorId }
            };

            _mockPostQueryRepository.Setup(x => x.GetPendingPostsAsync(query))
                .ReturnsAsync((posts, 1));

            // Mock Kafka to return empty list (User not found)
            _mockProducerRepository.Setup(x => x.ProduceGetAllAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User>());

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var dto = result.ResponseData.First();

            Assert.Equal(posts[0].PostId, dto.PostId);
            Assert.Null(dto.AuthorFirstName); // Should be null gracefully
        }

        [Fact]
        public async Task Handle_ShouldUseDefaultPagination_WhenInputIsInvalid()
        {
            // Arrange
            var query = new GetPendingPostsQuery(Page: -1, Size: -10); // Invalid pagination

            _mockPostQueryRepository.Setup(x => x.GetPendingPostsAsync(query))
                .ReturnsAsync((new List<Domain.Models.Post>(), 0));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result.Pagination);
            Assert.Equal(1, result.Pagination.Page); // Default to 1
            Assert.Equal(10, result.Pagination.PageSize); // Default to 10
        }

        [Fact]
        public async Task Handle_ShouldReturn500_WhenRepositoryThrowsException()
        {
            // Arrange
            var query = new GetPendingPostsQuery();

            // Mock Repo to throw unexpected error
            _mockPostQueryRepository.Setup(x => x.GetPendingPostsAsync(query))
                .ThrowsAsync(new Exception("DB Connection Lost"));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Contains("An error occurred", result.Message);
            Assert.Contains("DB Connection Lost", result.Message);
        }
    }
}
