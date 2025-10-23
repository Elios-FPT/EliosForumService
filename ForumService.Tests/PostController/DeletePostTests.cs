using ForumService.Contract.Shared;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ForumService.Contract.UseCases.Post.Command;
// No Request record needed for this specific test file

namespace ForumService.Tests.PostController
{
    public class DeletePostTests
    {
        private readonly Mock<ISender> _senderMock;
        private readonly Web.Controllers.Post.PostController _controller;
        private readonly Guid _hardcodedUserId = new Guid("3ea1d8be-846d-47eb-9961-7f7d32f37333"); // Match controller

        public DeletePostTests()
        {
            _senderMock = new Mock<ISender>();
            _controller = new Web.Controllers.Post.PostController(_senderMock.Object);

            // Mock HttpContext, although the header reading is commented out,
            // it's good practice for other tests or if it gets uncommented.
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = new DefaultHttpContext(),
            };
        }

        // Test Case 1: Happy Path - Successful Deletion
        [Fact]
        [Trait("Category", "DeletePost - HappyPath")]
        public async Task DeletePost_WhenSuccessful_ReturnsOkResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var expectedResponse = new BaseResponseDto<bool> { Status = 200, Message = "Post deleted successfully.", ResponseData = true };

            _senderMock.Setup(s => s.Send(It.Is<DeletePostCommand>(c => c.PostId == postId && c.RequesterId == _hardcodedUserId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.DeletePost(postId);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            Assert.Equal("Post deleted successfully.", result.Message);
            _senderMock.Verify(s => s.Send(It.Is<DeletePostCommand>(c => c.PostId == postId && c.RequesterId == _hardcodedUserId), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 2: Failure Path - Post Not Found
        [Fact]
        [Trait("Category", "DeletePost - Failure")]
        public async Task DeletePost_WhenPostNotFound_ReturnsNotFoundResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var expectedResponse = new BaseResponseDto<bool> { Status = 404, Message = "Post not found.", ResponseData = false };

            _senderMock.Setup(s => s.Send(It.Is<DeletePostCommand>(c => c.PostId == postId && c.RequesterId == _hardcodedUserId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.DeletePost(postId);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Post not found.", result.Message);
            _senderMock.Verify(s => s.Send(It.Is<DeletePostCommand>(c => c.PostId == postId && c.RequesterId == _hardcodedUserId), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 3: Failure Path - User Not Authorized (Handler returns 403)
        [Fact]
        [Trait("Category", "DeletePost - Failure")]
        public async Task DeletePost_WhenUserNotAuthorized_ReturnsForbiddenResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            // Simulate the handler returning 403, even though controller uses hardcoded ID
            var expectedResponse = new BaseResponseDto<bool> { Status = 403, Message = "You are not authorized to delete this post.", ResponseData = false };

            _senderMock.Setup(s => s.Send(It.Is<DeletePostCommand>(c => c.PostId == postId && c.RequesterId == _hardcodedUserId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.DeletePost(postId);

            // Assert
            Assert.Equal(403, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("You are not authorized to delete this post.", result.Message);
            _senderMock.Verify(s => s.Send(It.Is<DeletePostCommand>(c => c.PostId == postId && c.RequesterId == _hardcodedUserId), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 4: Failure Path - Handler Returns Internal Server Error
        [Fact]
        [Trait("Category", "DeletePost - Failure")]
        public async Task DeletePost_WhenHandlerReturnsInternalError_ReturnsInternalServerErrorResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var expectedResponse = new BaseResponseDto<bool> { Status = 500, Message = "Database error during delete.", ResponseData = false };

            _senderMock.Setup(s => s.Send(It.Is<DeletePostCommand>(c => c.PostId == postId && c.RequesterId == _hardcodedUserId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.DeletePost(postId);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Database error during delete.", result.Message);
        }

        // Test Case 5: Exception Path - Sender Throws Exception
        [Fact]
        [Trait("Category", "DeletePost - Exception")]
        public async Task DeletePost_WhenSenderThrowsException_ExceptionIsPropagated()
        {
            // Arrange
            var postId = Guid.NewGuid();
            _senderMock.Setup(s => s.Send(It.Is<DeletePostCommand>(c => c.PostId == postId && c.RequesterId == _hardcodedUserId), It.IsAny<CancellationToken>()))
                       .ThrowsAsync(new InvalidOperationException("Service unavailable"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.DeletePost(postId));
        }

        // Test Case 6: Mapping Check - Correct IDs Passed to Command
        [Fact]
        [Trait("Category", "DeletePost - Mapping")]
        public async Task DeletePost_ShouldPassCorrectPostIdAndHardcodedUserIdToCommand()
        {
            // Arrange
            var postId = Guid.NewGuid();
            DeletePostCommand? capturedCommand = null;

            _senderMock.Setup(s => s.Send(It.IsAny<DeletePostCommand>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<bool>>, CancellationToken>((cmd, token) => capturedCommand = cmd as DeletePostCommand)
                       .ReturnsAsync(new BaseResponseDto<bool> { Status = 200, ResponseData = true });

            // Act
            await _controller.DeletePost(postId);

            // Assert
            Assert.NotNull(capturedCommand);
            Assert.Equal(postId, capturedCommand.PostId);
            Assert.Equal(_hardcodedUserId, capturedCommand.RequesterId); // Verify hardcoded ID
        }

        // Test Case 7: Verification - Sender Called Once
        [Fact]
        [Trait("Category", "DeletePost - Verification")]
        public async Task DeletePost_ShouldCallSenderSendExactlyOnce()
        {
            // Arrange
            var postId = Guid.NewGuid();
            _senderMock.Setup(s => s.Send(It.IsAny<DeletePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new BaseResponseDto<bool> { Status = 200, ResponseData = true });

            // Act
            await _controller.DeletePost(postId);

            // Assert
            _senderMock.Verify(s => s.Send(It.IsAny<DeletePostCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 8: Verification - Correct Command Type Sent
        [Fact]
        [Trait("Category", "DeletePost - Verification")]
        public async Task DeletePost_ShouldSendCorrectCommandType()
        {
            // Arrange
            var postId = Guid.NewGuid();
            _senderMock.Setup(s => s.Send(It.IsAny<DeletePostCommand>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new BaseResponseDto<bool> { Status = 200, ResponseData = true });

            // Act
            await _controller.DeletePost(postId);

            // Assert
            // Verifies Send was called with an object of type DeletePostCommand
            _senderMock.Verify(s => s.Send(It.IsAny<DeletePostCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 9: Edge Case - Empty Guid for PostId
        [Fact]
        [Trait("Category", "DeletePost - EdgeCase")]
        public async Task DeletePost_WithEmptyGuidPostId_ShouldPassEmptyGuidToCommand()
        {
            // Arrange
            var postId = Guid.Empty;
            DeletePostCommand? capturedCommand = null;

            _senderMock.Setup(s => s.Send(It.IsAny<DeletePostCommand>(), It.IsAny<CancellationToken>()))
                      .Callback<IRequest<BaseResponseDto<bool>>, CancellationToken>((cmd, token) => capturedCommand = cmd as DeletePostCommand)
                      .ReturnsAsync(new BaseResponseDto<bool> { Status = 404 }); // Assume handler returns 404

            // Act
            await _controller.DeletePost(postId);

            // Assert
            Assert.NotNull(capturedCommand);
            Assert.Equal(Guid.Empty, capturedCommand.PostId);
            Assert.Equal(_hardcodedUserId, capturedCommand.RequesterId);
        }

        // Test Case 10: Data Check - Response Structure on Success
        [Fact]
        [Trait("Category", "DeletePost - DataCheck")]
        public async Task DeletePost_WhenSuccessful_ReturnsCorrectResponseStructure()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var expectedResponse = new BaseResponseDto<bool> { Status = 200, Message = "Success", ResponseData = true };

            _senderMock.Setup(s => s.Send(It.Is<DeletePostCommand>(c => c.PostId == postId && c.RequesterId == _hardcodedUserId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.DeletePost(postId);

            // Assert
            Assert.Equal(expectedResponse.Status, result.Status);
            Assert.Equal(expectedResponse.Message, result.Message);
            Assert.Equal(expectedResponse.ResponseData, result.ResponseData);
        }
    }
}
