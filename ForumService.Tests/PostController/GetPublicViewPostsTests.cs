using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.Post;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ForumService.Contract.UseCases.Post.Query;
using static ForumService.Contract.UseCases.Post.Request; // Add using for Request

namespace ForumService.Tests.PostController
{
    public class GetPublicViewPostsTests
    {
        private readonly Mock<ISender> _senderMock;
        private readonly Web.Controllers.Post.PostController _controller;

        public GetPublicViewPostsTests()
        {
            _senderMock = new Mock<ISender>();
            _controller = new Web.Controllers.Post.PostController(_senderMock.Object);

            // No need to mock HttpContext as this API doesn't read headers
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = new DefaultHttpContext(),
            };
        }

        // Test Case 1
        [Fact]
        [Trait("Category", "GetPublicViewPosts - HappyPath")]
        public async Task GetPublicViewPosts_WithValidRequest_ReturnsSuccessWithData()
        {
            // Arrange
            var request = new GetPublishedPostsRequest(); // Use default request
            var expectedPosts = new List<PostViewDto> { new PostViewDto { PostId = Guid.NewGuid(), Title = "Test Post" } };
            var expectedResponse = new BaseResponseDto<IEnumerable<PostViewDto>> { Status = 200, ResponseData = expectedPosts };

            _senderMock.Setup(s => s.Send(It.IsAny<GetPublicViewPostsQuery>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetPublicViewPosts(request);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.Single(result.ResponseData);
            Assert.Equal("Test Post", result.ResponseData.First().Title);
            _senderMock.Verify(s => s.Send(It.IsAny<GetPublicViewPostsQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 2
        [Fact]
        [Trait("Category", "GetPublicViewPosts - HappyPath")]
        public async Task GetPublicViewPosts_WithValidRequest_ReturnsSuccessWithEmptyList()
        {
            // Arrange
            var request = new GetPublishedPostsRequest();
            var expectedPosts = new List<PostViewDto>(); // Empty list
            var expectedResponse = new BaseResponseDto<IEnumerable<PostViewDto>> { Status = 200, Message = "No posts found.", ResponseData = expectedPosts };

            _senderMock.Setup(s => s.Send(It.IsAny<GetPublicViewPostsQuery>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetPublicViewPosts(request);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.Empty(result.ResponseData);
            Assert.Equal("No posts found.", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<GetPublicViewPostsQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 3
        [Fact]
        [Trait("Category", "GetPublicViewPosts - Mapping")]
        public async Task GetPublicViewPosts_ShouldMapAllRequestParametersToQuery()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var tags = new List<string> { "tag1", "tag2" };
            var request = new GetPublishedPostsRequest(
                AuthorId: authorId,
                CategoryId: categoryId,
                PostType: "Solution",
                SearchKeyword: "search",
                Tags: tags,
                Limit: 10,
                Offset: 5,
                SortBy: "ViewsCount",
                SortOrder: "ASC"
            );

            GetPublicViewPostsQuery? capturedQuery = null;
            _senderMock.Setup(s => s.Send(It.IsAny<GetPublicViewPostsQuery>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<IEnumerable<PostViewDto>>>, CancellationToken>((q, token) => capturedQuery = q as GetPublicViewPostsQuery)
                       .ReturnsAsync(new BaseResponseDto<IEnumerable<PostViewDto>> { Status = 200, ResponseData = new List<PostViewDto>() });

            // Act
            await _controller.GetPublicViewPosts(request);

            // Assert
            Assert.NotNull(capturedQuery);
            Assert.Equal(request.AuthorId, capturedQuery.AuthorId);
            Assert.Equal(request.CategoryId, capturedQuery.CategoryId);
            Assert.Equal(request.PostType, capturedQuery.PostType);
            Assert.Equal(request.SearchKeyword, capturedQuery.SearchKeyword);
            Assert.Equal(request.Limit, capturedQuery.Limit);
            Assert.Equal(request.Offset, capturedQuery.Offset);
            Assert.Equal(request.Tags, capturedQuery.Tags);
            Assert.Equal(request.SortBy, capturedQuery.SortBy);
            Assert.Equal(request.SortOrder, capturedQuery.SortOrder);
        }

        // Test Case 4
        [Fact]
        [Trait("Category", "GetPublicViewPosts - Failure")]
        public async Task GetPublicViewPosts_HandlerReturnsInternalError_ControllerReturnsSame()
        {
            // Arrange
            var request = new GetPublishedPostsRequest();
            var expectedResponse = new BaseResponseDto<IEnumerable<PostViewDto>> { Status = 500, Message = "Database error", ResponseData = Enumerable.Empty<PostViewDto>() };

            _senderMock.Setup(s => s.Send(It.IsAny<GetPublicViewPostsQuery>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetPublicViewPosts(request);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.Empty(result.ResponseData);
            Assert.Equal("Database error", result.Message);
        }

        // Test Case 5
        [Fact]
        [Trait("Category", "GetPublicViewPosts - Exception")]
        public async Task GetPublicViewPosts_SenderThrowsException_ExceptionIsPropagated()
        {
            // Arrange
            var request = new GetPublishedPostsRequest();

            _senderMock.Setup(s => s.Send(It.IsAny<GetPublicViewPostsQuery>(), It.IsAny<CancellationToken>()))
                       .ThrowsAsync(new InvalidOperationException("Something went wrong"));

            // Act & Assert
            // In ASP.NET Core, exceptions are typically handled by middleware which returns 500.
            // This test just verifies the exception propagates out of the controller.
            await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.GetPublicViewPosts(request));
        }

        // Test Case 6 (New)
        [Fact]
        [Trait("Category", "GetPublicViewPosts - Mapping - Defaults")]
        public async Task GetPublicViewPosts_WithDefaultRequest_ShouldMapDefaultValuesToQuery()
        {
            // Arrange
            var request = new GetPublishedPostsRequest(); // Empty request, uses default values

            GetPublicViewPostsQuery? capturedQuery = null;
            _senderMock.Setup(s => s.Send(It.IsAny<GetPublicViewPostsQuery>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<IEnumerable<PostViewDto>>>, CancellationToken>((q, token) => capturedQuery = q as GetPublicViewPostsQuery)
                       .ReturnsAsync(new BaseResponseDto<IEnumerable<PostViewDto>> { Status = 200, ResponseData = new List<PostViewDto>() });

            // Act
            await _controller.GetPublicViewPosts(request);

            // Assert
            Assert.NotNull(capturedQuery);
            Assert.Null(capturedQuery.AuthorId);
            Assert.Null(capturedQuery.CategoryId);
            Assert.Null(capturedQuery.PostType);
            Assert.Null(capturedQuery.SearchKeyword);
            Assert.Equal(20, capturedQuery.Limit); // Default value from record
            Assert.Equal(0, capturedQuery.Offset);  // Default value from record
            Assert.Null(capturedQuery.Tags);
            Assert.Null(capturedQuery.SortBy);     // Default value from record
            Assert.Null(capturedQuery.SortOrder);  // Default value from record
        }

        // Test Case 7 (New)
        [Fact]
        [Trait("Category", "GetPublicViewPosts - Mapping - SortOrder")]
        public async Task GetPublicViewPosts_WithDescSortOrder_ShouldMapCorrectly()
        {
            // Arrange
            var request = new GetPublishedPostsRequest(SortOrder: "DESC");

            GetPublicViewPostsQuery? capturedQuery = null;
            _senderMock.Setup(s => s.Send(It.IsAny<GetPublicViewPostsQuery>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<IEnumerable<PostViewDto>>>, CancellationToken>((q, token) => capturedQuery = q as GetPublicViewPostsQuery)
                       .ReturnsAsync(new BaseResponseDto<IEnumerable<PostViewDto>> { Status = 200, ResponseData = new List<PostViewDto>() });

            // Act
            await _controller.GetPublicViewPosts(request);

            // Assert
            Assert.NotNull(capturedQuery);
            Assert.Equal("DESC", capturedQuery.SortOrder);
        }

        // Test Case 8 (New)
        [Fact]
        [Trait("Category", "GetPublicViewPosts - Mapping - CaseInsensitiveSortOrder")]
        public async Task GetPublicViewPosts_WithLowercaseSortOrder_ShouldMapCorrectly()
        {
            // Arrange
            var request = new GetPublishedPostsRequest(SortOrder: "asc"); // Lowercase

            GetPublicViewPostsQuery? capturedQuery = null;
            _senderMock.Setup(s => s.Send(It.IsAny<GetPublicViewPostsQuery>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<IEnumerable<PostViewDto>>>, CancellationToken>((q, token) => capturedQuery = q as GetPublicViewPostsQuery)
                       .ReturnsAsync(new BaseResponseDto<IEnumerable<PostViewDto>> { Status = 200, ResponseData = new List<PostViewDto>() });

            // Act
            await _controller.GetPublicViewPosts(request);

            // Assert
            // Controller just forwards the value, Handler is responsible for case-insensitive handling if needed
            Assert.NotNull(capturedQuery);
            Assert.Equal("asc", capturedQuery.SortOrder);
        }

        // Test Case 9 (New) - Test 400 Bad Request (example)
        // Note: The controller currently has no validation logic, so this test assumes the handler returns 400
        [Fact]
        [Trait("Category", "GetPublicViewPosts - Failure")]
        public async Task GetPublicViewPosts_HandlerReturnsBadRequest_ControllerReturnsSame()
        {
            // Arrange
            var request = new GetPublishedPostsRequest(Limit: -1); // Invalid Limit value
            var expectedResponse = new BaseResponseDto<IEnumerable<PostViewDto>> { Status = 400, Message = "Limit must be positive", ResponseData = Enumerable.Empty<PostViewDto>() };

            _senderMock.Setup(s => s.Send(It.Is<GetPublicViewPostsQuery>(q => q.Limit == -1), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetPublicViewPosts(request);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.Empty(result.ResponseData);
            Assert.Equal("Limit must be positive", result.Message);
        }

        // Test Case 10 (New) - Mapping with null/empty values
        [Fact]
        [Trait("Category", "GetPublicViewPosts - Mapping - Nulls")]
        public async Task GetPublicViewPosts_WithNullFilters_ShouldMapNullsToQuery()
        {
            // Arrange
            var request = new GetPublishedPostsRequest(
                AuthorId: null,
                CategoryId: null,
                PostType: null,
                SearchKeyword: "", // Empty string
                Tags: new List<string>(), // Empty list
                SortBy: null,
                SortOrder: null
            );

            GetPublicViewPostsQuery? capturedQuery = null;
            _senderMock.Setup(s => s.Send(It.IsAny<GetPublicViewPostsQuery>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<IEnumerable<PostViewDto>>>, CancellationToken>((q, token) => capturedQuery = q as GetPublicViewPostsQuery)
                       .ReturnsAsync(new BaseResponseDto<IEnumerable<PostViewDto>> { Status = 200, ResponseData = new List<PostViewDto>() });

            // Act
            await _controller.GetPublicViewPosts(request);

            // Assert
            Assert.NotNull(capturedQuery);
            Assert.Null(capturedQuery.AuthorId);
            Assert.Null(capturedQuery.CategoryId);
            Assert.Null(capturedQuery.PostType);
            Assert.Equal("", capturedQuery.SearchKeyword); // Empty string mapped correctly
            Assert.Empty(capturedQuery.Tags); // Empty list mapped correctly
            Assert.Null(capturedQuery.SortBy);
            Assert.Null(capturedQuery.SortOrder);
        }
    }
}

