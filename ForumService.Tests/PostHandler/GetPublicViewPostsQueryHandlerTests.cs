using ForumService.Contract.Models;
using ForumService.Core.Handler.Post.Query;
using ForumService.Core.Interfaces;
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

    public class GetPublicViewPostsQueryHandlerTests
    {
        private readonly Mock<IPostQueryRepository> _postQueryRepoMock;
        private readonly Mock<IKafkaProducerRepository<User>> _producerRepoMock;
        private readonly Mock<IGenericRepository<Domain.Models.Category>> _categoryRepoMock;
        private readonly GetPublicViewPostsQueryHandler _handler;

        public GetPublicViewPostsQueryHandlerTests()
        {
            _postQueryRepoMock = new Mock<IPostQueryRepository>();
            _producerRepoMock = new Mock<IKafkaProducerRepository<User>>();
            _categoryRepoMock = new Mock<IGenericRepository<Domain.Models.Category>>();

            _handler = new GetPublicViewPostsQueryHandler(
                _postQueryRepoMock.Object,
                _producerRepoMock.Object,
                _categoryRepoMock.Object
            );
        }

        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_WithPostsAndUsers_ReturnsSuccessWithMappedData()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var query = new GetPublicViewPostsQuery(Page: 1, Size: 10);

            var postsFromDb = new List<Domain.Models.Post>
            {
                new Domain.Models.Post
                {
                    PostId = Guid.NewGuid(),
                    AuthorId = authorId,
                    CategoryId = categoryId,
                    Title = "Integration Test Post",
                    Content = "Content here",
                    Category = new Domain.Models.Category
                    {
                        CategoryId = categoryId,
                        Name = "Technology"
                    }
                }
            };

            _postQueryRepoMock.Setup(r => r.GetPublicViewPostsAsync(It.IsAny<GetPublicViewPostsQuery>()))
                .ReturnsAsync((postsFromDb, 15));

            var usersFromKafka = new List<User>
            {
                new User { id = authorId, firstName = "John", lastName = "Doe", avatarUrl = "http://avatar.com/img.png" }
            };

            _producerRepoMock.Setup(p => p.ProduceGetAllAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),           
                    It.IsAny<CancellationToken>() 
                ))
                .ReturnsAsync(usersFromKafka);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Single(result.ResponseData);
            Assert.Equal("John", result.ResponseData.First().AuthorFirstName);
            Assert.Equal(15, result.Pagination.TotalItems);
        }

        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_WithNoPosts_ReturnsEmptyList()
        {
            // Arrange
            var query = new GetPublicViewPostsQuery();

            _postQueryRepoMock.Setup(r => r.GetPublicViewPostsAsync(It.IsAny<GetPublicViewPostsQuery>()))
                .ReturnsAsync((Enumerable.Empty<Domain.Models.Post>(), 0));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Empty(result.ResponseData);

            _producerRepoMock.Verify(p => p.ProduceGetAllAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ), Times.Never);
        }

        [Fact]
        [Trait("Category", "Handler - Resilience")]
        public async Task Handle_WhenKafkaFails_ReturnsPostsWithNullAuthorInfo()
        {
            // Arrange
            var query = new GetPublicViewPostsQuery();
            var postsFromDb = new List<Domain.Models.Post>
            {
                new Domain.Models.Post { PostId = Guid.NewGuid(), Title = "Post 1" }
            };

            _postQueryRepoMock.Setup(r => r.GetPublicViewPostsAsync(It.IsAny<GetPublicViewPostsQuery>()))
                .ReturnsAsync((postsFromDb, 1));

            _producerRepoMock.Setup(p => p.ProduceGetAllAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ))
                .ThrowsAsync(new Exception("Kafka Timeout"));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Null(result.ResponseData.First().AuthorFirstName);
        }
    }
}
