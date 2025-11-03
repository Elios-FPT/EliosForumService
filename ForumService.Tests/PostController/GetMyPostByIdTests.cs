using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.Post;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ForumService.Contract.UseCases.Post.Query; 

namespace ForumService.Tests.PostController
{
    public class GetMyPostByIdTests
    {
        private readonly Mock<ISender> _senderMock;
        private readonly Web.Controllers.Post.PostController _controller;
        private readonly Mock<HttpContext> _httpContextMock;
        private readonly Mock<HttpRequest> _httpRequestMock;
        private readonly HeaderDictionary _headers;

        public GetMyPostByIdTests()
        {
            _senderMock = new Mock<ISender>();
            _controller = new Web.Controllers.Post.PostController(_senderMock.Object);

            // Mock HttpContext and Headers
            _httpContextMock = new Mock<HttpContext>();
            _httpRequestMock = new Mock<HttpRequest>();
            _headers = new HeaderDictionary();
            _httpRequestMock.Setup(x => x.Headers).Returns(_headers);
            _httpContextMock.Setup(x => x.Request).Returns(_httpRequestMock.Object);

            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = _httpContextMock.Object,
            };
        }

        // Helper to set the auth header
        private void SetAuthHeader(string? value)
        {
            if (value != null)
            {
                _headers["X-Auth-Request-User"] = new StringValues(value);
            }
            else
            {
                _headers.Remove("X-Auth-Request-User");
            }
        }

        // Test Case 1: Happy Path - Successful Retrieval
        [Fact]
        [Trait("Category", "GetMyPostById - HappyPath")]
        public async Task GetMyPostById_WhenSuccessful_ReturnsOkResponseWithData()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var postDetailDto = new PostViewDetailDto { PostId = postId, AuthorId = userId, Title = "My Test Post" };
            var expectedResponse = new BaseResponseDto<PostViewDetailDto> { Status = 200, Message = "Post retrieved.", ResponseData = postDetailDto };

            _senderMock.Setup(s => s.Send(It.Is<GetMyPostByIdQuery>(q => q.PostId == postId && q.RequesterId == userId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetMyPostById(postId);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.Equal(postId, result.ResponseData.PostId);
            Assert.Equal("My Test Post", result.ResponseData.Title);
            _senderMock.Verify(s => s.Send(It.IsAny<GetMyPostByIdQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 2: Authentication Failure - Missing Header
        [Fact]
        [Trait("Category", "GetMyPostById - AuthFailure")]
        public async Task GetMyPostById_WhenAuthHeaderMissing_ReturnsUnauthorized()
        {
            // Arrange
            var postId = Guid.NewGuid();
            SetAuthHeader(null); // No header

            // Act
            var result = await _controller.GetMyPostById(postId);

            // Assert
            Assert.Equal(401, result.Status);
            Assert.Null(result.ResponseData);
            Assert.Equal("User not authenticated or invalid/missing X-Auth-Request-User header", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<GetMyPostByIdQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // Test Case 3: Authentication Failure - Invalid Header Format
        [Fact]
        [Trait("Category", "GetMyPostById - AuthFailure")]
        public async Task GetMyPostById_WhenAuthHeaderInvalidFormat_ReturnsUnauthorized()
        {
            // Arrange
            var postId = Guid.NewGuid();
            SetAuthHeader("invalid-guid"); // Invalid format

            // Act
            var result = await _controller.GetMyPostById(postId);

            // Assert
            Assert.Equal(401, result.Status);
            Assert.Null(result.ResponseData);
            Assert.Equal("User not authenticated or invalid/missing X-Auth-Request-User header", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<GetMyPostByIdQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // Test Case 4: Failure Path - Post Not Found or Not Owner (Handler returns 404)
        [Fact]
        [Trait("Category", "GetMyPostById - Failure")]
        public async Task GetMyPostById_WhenPostNotFoundOrNotOwner_ReturnsNotFoundResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var expectedResponse = new BaseResponseDto<PostViewDetailDto>
            {
                Status = 404,
                Message = "Post not found or you do not have permission to view it.",
                ResponseData = null
            };

            _senderMock.Setup(s => s.Send(It.IsAny<GetMyPostByIdQuery>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetMyPostById(postId);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Null(result.ResponseData);
            Assert.Equal(expectedResponse.Message, result.Message);
        }

        // Test Case 5: Failure Path - Handler Returns Internal Server Error
        [Fact]
        [Trait("Category", "GetMyPostById - Failure")]
        public async Task GetMyPostById_WhenHandlerReturnsInternalError_ReturnsInternalServerErrorResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var expectedResponse = new BaseResponseDto<PostViewDetailDto> { Status = 500, Message = "Database error.", ResponseData = null };

            _senderMock.Setup(s => s.Send(It.IsAny<GetMyPostByIdQuery>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetMyPostById(postId);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Null(result.ResponseData);
            Assert.Equal("Database error.", result.Message);
        }

        // Test Case 6: Exception Path - Sender Throws Exception
        [Fact]
        [Trait("Category", "GetMyPostById - Exception")]
        public async Task GetMyPostById_WhenSenderThrowsException_ExceptionIsPropagated()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());

            _senderMock.Setup(s => s.Send(It.IsAny<GetMyPostByIdQuery>(), It.IsAny<CancellationToken>()))
                       .ThrowsAsync(new TimeoutException("Operation timed out"));

            // Act & Assert
            // We assert that the exception propagates up, as this controller action doesn't have a try-catch
            await Assert.ThrowsAsync<TimeoutException>(() => _controller.GetMyPostById(postId));
        }

        // Test Case 7: Mapping Check - Correct IDs Passed to Query
        [Fact]
        [Trait("Category", "GetMyPostById - Mapping")]
        public async Task GetMyPostById_ShouldPassCorrectIdsToQuery()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            GetMyPostByIdQuery? capturedQuery = null;

            _senderMock.Setup(s => s.Send(It.IsAny<GetMyPostByIdQuery>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<PostViewDetailDto>>, CancellationToken>((query, token) => capturedQuery = query as GetMyPostByIdQuery)
                       .ReturnsAsync(new BaseResponseDto<PostViewDetailDto> { Status = 200, ResponseData = new PostViewDetailDto() });

            // Act
            await _controller.GetMyPostById(postId);

            // Assert
            Assert.NotNull(capturedQuery);
            Assert.Equal(postId, capturedQuery.PostId);
            Assert.Equal(userId, capturedQuery.RequesterId);
        }
    }
}
