using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.Post;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ForumService.Contract.UseCases.Post.Query;
using static ForumService.Contract.UseCases.Post.Request;

namespace ForumService.Tests.PostController
{
    public class GetMyPostsTests
    {
        private readonly Mock<ISender> _senderMock;
        private readonly Web.Controllers.Post.PostController _controller;
        private readonly Mock<HttpContext> _httpContextMock;
        private readonly Mock<HttpRequest> _httpRequestMock;
        private readonly HeaderDictionary _headers;

        public GetMyPostsTests()
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

        // Test Case 1: Happy Path - User Authenticated, Posts Found
        [Fact]
        [Trait("Category", "GetMyPosts - HappyPath")]
        public async Task GetMyPosts_WhenUserAuthenticatedAndPostsExist_ReturnsSuccessWithData()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var request = new GetMyPostsRequest();
            var expectedPosts = new List<PostViewDto> { new PostViewDto { PostId = Guid.NewGuid(), Title = "My Post" } };
            var expectedResponse = new BaseResponseDto<IEnumerable<PostViewDto>> { Status = 200, ResponseData = expectedPosts };

            _senderMock.Setup(s => s.Send(It.Is<GetMyPostsQuery>(q => q.RequesterId == userId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetMyPosts(request);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.Single(result.ResponseData);
            Assert.Equal("My Post", result.ResponseData.First().Title);
            _senderMock.Verify(s => s.Send(It.Is<GetMyPostsQuery>(q => q.RequesterId == userId), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 2: Happy Path - User Authenticated, No Posts Found
        [Fact]
        [Trait("Category", "GetMyPosts - HappyPath")]
        public async Task GetMyPosts_WhenUserAuthenticatedAndNoPostsExist_ReturnsSuccessWithEmptyList()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var request = new GetMyPostsRequest();
            var expectedPosts = new List<PostViewDto>();
            var expectedResponse = new BaseResponseDto<IEnumerable<PostViewDto>> { Status = 200, Message = "No posts found.", ResponseData = expectedPosts };

            _senderMock.Setup(s => s.Send(It.Is<GetMyPostsQuery>(q => q.RequesterId == userId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetMyPosts(request);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.Empty(result.ResponseData);
            Assert.Equal("No posts found.", result.Message);
            _senderMock.Verify(s => s.Send(It.Is<GetMyPostsQuery>(q => q.RequesterId == userId), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 3: Authentication Failure - Missing Header
        [Fact]
        [Trait("Category", "GetMyPosts - AuthFailure")]
        public async Task GetMyPosts_WhenAuthHeaderMissing_ReturnsUnauthorized()
        {
            // Arrange
            SetAuthHeader(null); // No header
            var request = new GetMyPostsRequest();

            // Act
            var result = await _controller.GetMyPosts(request);

            // Assert
            Assert.Equal(401, result.Status);
            Assert.Contains("User not authenticated", result.Message);
            Assert.NotNull(result.ResponseData);
            Assert.Empty(result.ResponseData); // Ensure data is empty on error
            _senderMock.Verify(s => s.Send(It.IsAny<GetMyPostsQuery>(), It.IsAny<CancellationToken>()), Times.Never); // Sender should not be called
        }

        // Test Case 4: Authentication Failure - Invalid Header Format
        [Fact]
        [Trait("Category", "GetMyPosts - AuthFailure")]
        public async Task GetMyPosts_WhenAuthHeaderInvalidFormat_ReturnsUnauthorized()
        {
            // Arrange
            SetAuthHeader("not-a-guid"); // Invalid Guid
            var request = new GetMyPostsRequest();

            // Act
            var result = await _controller.GetMyPosts(request);

            // Assert
            Assert.Equal(401, result.Status);
            Assert.Contains("User not authenticated", result.Message);
            Assert.NotNull(result.ResponseData);
            Assert.Empty(result.ResponseData);
            _senderMock.Verify(s => s.Send(It.IsAny<GetMyPostsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // Test Case 5: Mapping Check - All Parameters
        [Fact]
        [Trait("Category", "GetMyPosts - Mapping")]
        public async Task GetMyPosts_ShouldMapAllRequestParametersAndUserIdToQuery()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var categoryId = Guid.NewGuid();
            var request = new GetMyPostsRequest(
                Status: "Draft",
                CategoryId: categoryId,
                PostType: "Post",
                SearchKeyword: "test",
                Limit: 15,
                Offset: 10,
                SortBy: "CreatedAt",
                SortOrder: "ASC"
            );

            GetMyPostsQuery? capturedQuery = null;
            _senderMock.Setup(s => s.Send(It.IsAny<GetMyPostsQuery>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<IEnumerable<PostViewDto>>>, CancellationToken>((q, token) => capturedQuery = q as GetMyPostsQuery)
                       .ReturnsAsync(new BaseResponseDto<IEnumerable<PostViewDto>> { Status = 200, ResponseData = new List<PostViewDto>() });

            // Act
            await _controller.GetMyPosts(request);

            // Assert
            Assert.NotNull(capturedQuery);
            Assert.Equal(userId, capturedQuery.RequesterId); // Verify UserId from header
            Assert.Equal(request.Status, capturedQuery.Status);
            Assert.Equal(request.CategoryId, capturedQuery.CategoryId);
            Assert.Equal(request.PostType, capturedQuery.PostType);
            Assert.Equal(request.SearchKeyword, capturedQuery.SearchKeyword);
            Assert.Equal(request.Limit, capturedQuery.Limit);
            Assert.Equal(request.Offset, capturedQuery.Offset);
            Assert.Equal(request.SortBy, capturedQuery.SortBy);
            Assert.Equal(request.SortOrder, capturedQuery.SortOrder);
        }

        // Test Case 6: Mapping Check - Default Values
        [Fact]
        [Trait("Category", "GetMyPosts - Mapping - Defaults")]
        public async Task GetMyPosts_WithDefaultRequest_ShouldMapDefaultValuesAndUserIdToQuery()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var request = new GetMyPostsRequest(); // Uses default values from record

            GetMyPostsQuery? capturedQuery = null;
            _senderMock.Setup(s => s.Send(It.IsAny<GetMyPostsQuery>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<IEnumerable<PostViewDto>>>, CancellationToken>((q, token) => capturedQuery = q as GetMyPostsQuery)
                       .ReturnsAsync(new BaseResponseDto<IEnumerable<PostViewDto>> { Status = 200, ResponseData = new List<PostViewDto>() });

            // Act
            await _controller.GetMyPosts(request);

            // Assert
            Assert.NotNull(capturedQuery);
            Assert.Equal(userId, capturedQuery.RequesterId);
            Assert.Null(capturedQuery.Status);      // Default
            Assert.Null(capturedQuery.CategoryId); // Default
            Assert.Null(capturedQuery.PostType);     // Default
            Assert.Null(capturedQuery.SearchKeyword);// Default
            Assert.Equal(20, capturedQuery.Limit);   // Default
            Assert.Equal(0, capturedQuery.Offset);   // Default
            Assert.Null(capturedQuery.SortBy);     // Default
            Assert.Null(capturedQuery.SortOrder);  // Default
        }

        // Test Case 7: Failure - Handler Returns Internal Server Error
        [Fact]
        [Trait("Category", "GetMyPosts - Failure")]
        public async Task GetMyPosts_WhenHandlerReturnsInternalError_ControllerReturnsSame()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var request = new GetMyPostsRequest();
            var expectedResponse = new BaseResponseDto<IEnumerable<PostViewDto>> { Status = 500, Message = "Failed to query database", ResponseData = Enumerable.Empty<PostViewDto>() };

            _senderMock.Setup(s => s.Send(It.Is<GetMyPostsQuery>(q => q.RequesterId == userId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetMyPosts(request);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.Empty(result.ResponseData);
            Assert.Equal("Failed to query database", result.Message);
        }

        // Test Case 8: Exception - Sender Throws Exception
        [Fact]
        [Trait("Category", "GetMyPosts - Exception")]
        public async Task GetMyPosts_WhenSenderThrowsException_ExceptionIsPropagated()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var request = new GetMyPostsRequest();

            _senderMock.Setup(s => s.Send(It.Is<GetMyPostsQuery>(q => q.RequesterId == userId), It.IsAny<CancellationToken>()))
                       .ThrowsAsync(new TimeoutException("Kafka timed out"));

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(() => _controller.GetMyPosts(request));
        }

        // Test Case 9: Verification - Sender Call Count on Auth Success
        [Fact]
        [Trait("Category", "GetMyPosts - Verification")]
        public async Task GetMyPosts_WhenUserAuthenticated_ShouldCallSenderSendExactlyOnce()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var request = new GetMyPostsRequest();
            _senderMock.Setup(s => s.Send(It.IsAny<GetMyPostsQuery>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new BaseResponseDto<IEnumerable<PostViewDto>> { Status = 200, ResponseData = new List<PostViewDto>() });

            // Act
            await _controller.GetMyPosts(request);

            // Assert
            _senderMock.Verify(s => s.Send(It.IsAny<GetMyPostsQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 10: Verification - Sender Not Called on Auth Failure
        [Fact]
        [Trait("Category", "GetMyPosts - Verification")]
        public async Task GetMyPosts_WhenUserNotAuthenticated_ShouldNotCallSenderSend()
        {
            // Arrange
            SetAuthHeader(null); // No auth header
            var request = new GetMyPostsRequest();

            // Act
            await _controller.GetMyPosts(request);

            // Assert
            _senderMock.Verify(s => s.Send(It.IsAny<GetMyPostsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
