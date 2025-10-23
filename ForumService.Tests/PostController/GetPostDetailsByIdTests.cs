using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.Post;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ForumService.Contract.UseCases.Post.Query;
using static ForumService.Contract.UseCases.Post.Request; // Add using for Request if needed

namespace ForumService.Tests.PostController
{
    public class GetPostDetailsByIdTests
    {
        private readonly Mock<ISender> _senderMock;
        private readonly Web.Controllers.Post.PostController _controller;

        public GetPostDetailsByIdTests()
        {
            _senderMock = new Mock<ISender>();
            _controller = new Web.Controllers.Post.PostController(_senderMock.Object);

            // Mock HttpContext if needed for authentication/headers (not strictly necessary for this specific action)
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = new DefaultHttpContext(),
            };
        }

        // Test Case 1: Happy Path - Post Found
        [Fact]
        [Trait("Category", "GetPostDetailsById - HappyPath")]
        public async Task GetPostDetailsById_WhenPostExists_ReturnsSuccessWithData()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var expectedPostDetail = new PostViewDetailDto { PostId = postId, Title = "Existing Post" };
            var expectedResponse = new BaseResponseDto<PostViewDetailDto> { Status = 200, ResponseData = expectedPostDetail };

            _senderMock.Setup(s => s.Send(It.Is<GetPostDetailsByIdQuery>(q => q.PostId == postId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetPostDetailsById(postId);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.Equal(postId, result.ResponseData.PostId);
            Assert.Equal("Existing Post", result.ResponseData.Title);
            _senderMock.Verify(s => s.Send(It.Is<GetPostDetailsByIdQuery>(q => q.PostId == postId), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 2: Happy Path - Post Not Found
        [Fact]
        [Trait("Category", "GetPostDetailsById - HappyPath")]
        public async Task GetPostDetailsById_WhenPostDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var expectedResponse = new BaseResponseDto<PostViewDetailDto> { Status = 404, Message = "Post not found.", ResponseData = null };

            _senderMock.Setup(s => s.Send(It.Is<GetPostDetailsByIdQuery>(q => q.PostId == postId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetPostDetailsById(postId);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Null(result.ResponseData);
            Assert.Equal("Post not found.", result.Message);
            _senderMock.Verify(s => s.Send(It.Is<GetPostDetailsByIdQuery>(q => q.PostId == postId), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 3: Mapping Check - Correct PostId Passed to Query
        [Fact]
        [Trait("Category", "GetPostDetailsById - Mapping")]
        public async Task GetPostDetailsById_ShouldPassCorrectPostIdToQuery()
        {
            // Arrange
            var postId = Guid.NewGuid();
            GetPostDetailsByIdQuery? capturedQuery = null;

            _senderMock.Setup(s => s.Send(It.IsAny<GetPostDetailsByIdQuery>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<PostViewDetailDto>>, CancellationToken>((q, token) => capturedQuery = q as GetPostDetailsByIdQuery)
                       .ReturnsAsync(new BaseResponseDto<PostViewDetailDto> { Status = 404 }); // Return 404 just to complete the call

            // Act
            await _controller.GetPostDetailsById(postId);

            // Assert
            Assert.NotNull(capturedQuery);
            Assert.Equal(postId, capturedQuery.PostId);
        }

        // Test Case 4: Failure - Handler Returns Internal Server Error
        [Fact]
        [Trait("Category", "GetPostDetailsById - Failure")]
        public async Task GetPostDetailsById_WhenHandlerReturnsInternalError_ControllerReturnsSame()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var expectedResponse = new BaseResponseDto<PostViewDetailDto> { Status = 500, Message = "Database connection error", ResponseData = null };

            _senderMock.Setup(s => s.Send(It.Is<GetPostDetailsByIdQuery>(q => q.PostId == postId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetPostDetailsById(postId);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Null(result.ResponseData);
            Assert.Equal("Database connection error", result.Message);
        }

        // Test Case 5: Exception - Sender Throws Exception
        [Fact]
        [Trait("Category", "GetPostDetailsById - Exception")]
        public async Task GetPostDetailsById_WhenSenderThrowsException_ExceptionIsPropagated()
        {
            // Arrange
            var postId = Guid.NewGuid();
            _senderMock.Setup(s => s.Send(It.Is<GetPostDetailsByIdQuery>(q => q.PostId == postId), It.IsAny<CancellationToken>()))
                       .ThrowsAsync(new InvalidOperationException("Critical failure"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.GetPostDetailsById(postId));
        }

        // Test Case 6: Edge Case - Empty Guid (though typically prevented by routing/model binding)
        [Fact]
        [Trait("Category", "GetPostDetailsById - EdgeCase")]
        public async Task GetPostDetailsById_WithEmptyGuid_PassesEmptyGuidToQuery()
        {
            // Arrange
            var postId = Guid.Empty;
            GetPostDetailsByIdQuery? capturedQuery = null;

            _senderMock.Setup(s => s.Send(It.IsAny<GetPostDetailsByIdQuery>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<PostViewDetailDto>>, CancellationToken>((q, token) => capturedQuery = q as GetPostDetailsByIdQuery)
                       .ReturnsAsync(new BaseResponseDto<PostViewDetailDto> { Status = 404 }); // Assume handler returns 404 for empty guid

            // Act
            await _controller.GetPostDetailsById(postId);

            // Assert
            Assert.NotNull(capturedQuery);
            Assert.Equal(Guid.Empty, capturedQuery.PostId);
        }

        // Test Case 7: Data Check - ResponseData Not Null on Success
        [Fact]
        [Trait("Category", "GetPostDetailsById - DataCheck")]
        public async Task GetPostDetailsById_WhenSuccessful_ResponseDataShouldNotBeNull()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var expectedPostDetail = new PostViewDetailDto { PostId = postId, Title = "Data Check Post" };
            var expectedResponse = new BaseResponseDto<PostViewDetailDto> { Status = 200, ResponseData = expectedPostDetail };

            _senderMock.Setup(s => s.Send(It.Is<GetPostDetailsByIdQuery>(q => q.PostId == postId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetPostDetailsById(postId);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
        }

        // Test Case 8: Data Check - ResponseData Null on NotFound
        [Fact]
        [Trait("Category", "GetPostDetailsById - DataCheck")]
        public async Task GetPostDetailsById_WhenNotFound_ResponseDataShouldBeNull()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var expectedResponse = new BaseResponseDto<PostViewDetailDto> { Status = 404, ResponseData = null };

            _senderMock.Setup(s => s.Send(It.Is<GetPostDetailsByIdQuery>(q => q.PostId == postId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetPostDetailsById(postId);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Null(result.ResponseData);
        }

        // Test Case 9: Verify Sender Call Count
        [Fact]
        [Trait("Category", "GetPostDetailsById - Verification")]
        public async Task GetPostDetailsById_ShouldCallSenderSendExactlyOnce()
        {
            // Arrange
            var postId = Guid.NewGuid();
            _senderMock.Setup(s => s.Send(It.IsAny<GetPostDetailsByIdQuery>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new BaseResponseDto<PostViewDetailDto> { Status = 404 }); // Response doesn't matter for this test

            // Act
            await _controller.GetPostDetailsById(postId);

            // Assert
            _senderMock.Verify(s => s.Send(It.IsAny<GetPostDetailsByIdQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 10: Verify Query Type
        [Fact]
        [Trait("Category", "GetPostDetailsById - Verification")]
        public async Task GetPostDetailsById_ShouldSendCorrectQueryType()
        {
            // Arrange
            var postId = Guid.NewGuid();
            _senderMock.Setup(s => s.Send(It.IsAny<GetPostDetailsByIdQuery>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new BaseResponseDto<PostViewDetailDto> { Status = 404 });

            // Act
            await _controller.GetPostDetailsById(postId);

            // Assert
            // Verifies that Send was called with an object of type GetPostDetailsByIdQuery
            _senderMock.Verify(s => s.Send(It.IsAny<GetPostDetailsByIdQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
