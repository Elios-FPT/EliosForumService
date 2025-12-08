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
    public class GetArchivedPostsQueryHandlerTests
    {
        // Mocks
        private readonly Mock<IPostQueryRepository> _mockPostQueryRepository;
        private readonly Mock<IKafkaProducerRepository<User>> _mockProducerRepository;

        // System Under Test
        private readonly GetArchivedPostsQueryHandler _handler;

        public GetArchivedPostsQueryHandlerTests()
        {
            _mockPostQueryRepository = new Mock<IPostQueryRepository>();
            _mockProducerRepository = new Mock<IKafkaProducerRepository<User>>();

            _handler = new GetArchivedPostsQueryHandler(
                _mockPostQueryRepository.Object,
                _mockProducerRepository.Object
            );
        }

        [Fact]
        public async Task Handle_ShouldReturnEmptyList_WhenNoPostsFound()
        {
            // Arrange
            var query = new GetArchivedPostsQuery();

            // Mock Repo to return empty list
            _mockPostQueryRepository.Setup(x => x.GetArchivedPostsAsync(query))
                .ReturnsAsync((Enumerable.Empty<Domain.Models.Post>(), 0));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.ResponseData);
            Assert.Equal("No archived posts found.", result.Message);

            // Corrected: Accessing Pagination property
            Assert.NotNull(result.Pagination);
            Assert.Equal(0, result.Pagination.TotalItems);

            // Verify Kafka was NOT called because no posts exist
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
            var moderatorId = Guid.NewGuid();
            var deleterId = Guid.NewGuid();

            var query = new GetArchivedPostsQuery();

            var posts = new List<Domain.Models.Post>
            {
                new Domain.Models.Post
                {
                    PostId = Guid.NewGuid(),
                    Title = "Deleted Post",
                    AuthorId = authorId,
                    ModeratedBy = moderatorId,
                    DeletedBy = deleterId,
                    Category = new Category { Name = "Tech" }
                }
            };

            // Mock Post Repo
            _mockPostQueryRepository.Setup(x => x.GetArchivedPostsAsync(query))
                .ReturnsAsync((posts, 1));

            // Mock User Service (Kafka) to return profiles
            var users = new List<User>
            {
                new User { id = authorId, firstName = "John", lastName = "Doe", avatarUrl = "author.png" },
                new User { id = moderatorId, firstName = "Mod", lastName = "Admin", avatarUrl = "mod.png" },
                new User { id = deleterId, firstName = "Super", lastName = "Mod", avatarUrl = "deleter.png" }
            };

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
            Assert.Equal("Tech", dto.CategoryName);
            Assert.Equal("John", dto.AuthorFirstName);
            Assert.Equal("Mod", dto.ModeratorFirstName);
            Assert.Equal("Super", dto.DeletedByFirstName);
        }

        [Fact]
        public async Task Handle_ShouldReturnPostsWithoutUserDetails_WhenUserServiceFails()
        {
            // Arrange
            var query = new GetArchivedPostsQuery();
            var posts = new List<Domain.Models.Post>
            {
                new Domain.Models.Post { PostId = Guid.NewGuid(), AuthorId = Guid.NewGuid(), Title = "Post 1" }
            };

            _mockPostQueryRepository.Setup(x => x.GetArchivedPostsAsync(query))
                .ReturnsAsync((posts, 1));

            // Mock Kafka to throw exception (simulating service down)
            _mockProducerRepository.Setup(x => x.ProduceGetAllAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("User Service Unavailable"));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.ResponseData);
            Assert.Equal("Archived posts retrieved successfully.", result.Message); // Should still succeed

            var dto = result.ResponseData.First();
            Assert.Equal("Post 1", dto.Title);

            // User details should be null because the service failed, but the flow continued
            Assert.Null(dto.AuthorFirstName);
            Assert.Null(dto.AuthorLastName);
        }

        [Fact]
        public async Task Handle_ShouldHandlePartialUserMatches_WhenSomeUsersNotFound()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var query = new GetArchivedPostsQuery();
            var posts = new List<Domain.Models.Post>
            {
                new Domain.Models.Post { PostId = Guid.NewGuid(), AuthorId = authorId }
            };

            _mockPostQueryRepository.Setup(x => x.GetArchivedPostsAsync(query))
                .ReturnsAsync((posts, 1));

            // Mock Kafka to return empty list (User not found in user service)
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

            // Post data exists
            Assert.Equal(posts[0].PostId, dto.PostId);
            // Author data is null (graceful handling of missing dictionary key)
            Assert.Null(dto.AuthorFirstName);
        }

        [Fact]
        public async Task Handle_ShouldUseDefaultPagination_WhenInputIsInvalid()
        {
            // Arrange
            var query = new GetArchivedPostsQuery(Page: -5, Size: 0); // Invalid pagination

            // Mock Repo (Repo usually handles the query object, but the Handler sets response metadata)
            _mockPostQueryRepository.Setup(x => x.GetArchivedPostsAsync(query))
                .ReturnsAsync((new List<Domain.Models.Post>(), 0));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result.Pagination);
            // Corrected: Accessing Pagination property
            Assert.Equal(1, result.Pagination.Page);
            Assert.Equal(10, result.Pagination.PageSize);
        }

        [Fact]
        public async Task Handle_ShouldReturn500_WhenRepositoryThrowsException()
        {
            // Arrange
            var query = new GetArchivedPostsQuery();

            // Mock Repo to throw unexpected error
            _mockPostQueryRepository.Setup(x => x.GetArchivedPostsAsync(query))
                .ThrowsAsync(new Exception("Database Connection Error"));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Contains("An error occurred", result.Message);
            Assert.Contains("Database Connection Error", result.Message);
        }
    }
}
