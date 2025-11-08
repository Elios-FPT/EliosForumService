using ForumService.Contract.Shared;
using ForumService.Web.Controllers;
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
using ForumService.Contract.UseCases.Post;
using static ForumService.Contract.UseCases.Post.Request;
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Tests.PostController
{
    public class UpdatePostTests
    {
        private readonly Mock<ISender> _senderMock;
        private readonly Web.Controllers.Post.PostController _controller;
        private readonly Guid _testUserId = Guid.NewGuid();

        public UpdatePostTests()
        {
            _senderMock = new Mock<ISender>();
            _controller = new Web.Controllers.Post.PostController(_senderMock.Object);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["X-Auth-Request-User"] = _testUserId.ToString();
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext,
            };

        }


        [Fact]
        [Trait("Category", "UpdatePost - HappyPath")]
        public async Task UpdatePost_WithTextAndTags_ReturnsSuccess()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var tags = new List<string> { "tag1" };
            var request = new UpdatePostRequest(
                Title: "Updated Title",
                Summary: "Updated Summary",
                Content: "Updated Content",
                CategoryId: Guid.NewGuid(),
                Tags: tags
            );

            var expectedResponse = new BaseResponseDto<bool> { Status = 200, ResponseData = true };

            _senderMock.Setup(s => s.Send(It.IsAny<UpdatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdatePost(postId, request);

            // Assert
            Assert.Equal(200, result.Status);
            _senderMock.Verify(s => s.Send(It.Is<UpdatePostCommand>(c =>
                c.PostId == postId &&
                c.RequesterId == _testUserId &&
                c.Tags != null && c.Tags.Count == 1 
            ), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("Category", "UpdatePost - HappyPath")]
        public async Task UpdatePost_LinkNewAttachments_ReturnsSuccess()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var idsToLink = new List<Guid> { Guid.NewGuid() };
            var request = new UpdatePostRequest(    
                Title: "Title",
                Summary: "Summary",
                Content: "Content",
                CategoryId: null,
                Tags: null
            );

            var expectedResponse = new BaseResponseDto<bool> { Status = 200, ResponseData = true };

            _senderMock.Setup(s => s.Send(It.IsAny<UpdatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdatePost(postId, request);

            // Assert
            Assert.Equal(200, result.Status);
            _senderMock.Verify(s => s.Send(It.Is<UpdatePostCommand>(c =>
                c.PostId == postId &&
                c.RequesterId == _testUserId
            ), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("Category", "UpdatePost - HappyPath")]
        public async Task UpdatePost_DeleteExistingAttachments_ReturnsSuccess()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var idsToDelete = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            var request = new UpdatePostRequest(
                Title: "Title",
                Summary: "Summary",
                Content: "Content",
                CategoryId: null,
                Tags: null
            );

            var expectedResponse = new BaseResponseDto<bool> { Status = 200, ResponseData = true };

            _senderMock.Setup(s => s.Send(It.IsAny<UpdatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdatePost(postId, request);

            // Assert
            Assert.Equal(200, result.Status);
            _senderMock.Verify(s => s.Send(It.Is<UpdatePostCommand>(c =>
                c.PostId == postId &&
                c.RequesterId == _testUserId 
            ), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("Category", "UpdatePost - HappyPath")]
        public async Task UpdatePost_LinkAndAddDeleteFiles_ReturnsSuccess()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var idsToDelete = new List<Guid> { Guid.NewGuid() };
            var idsToLink = new List<Guid> { Guid.NewGuid() };
            var request = new UpdatePostRequest(
                Title: "Title",
                Summary: "Summary",
                Content: "Content",
                CategoryId: null,
                Tags: null
            );

            var expectedResponse = new BaseResponseDto<bool> { Status = 200, ResponseData = true };

            _senderMock.Setup(s => s.Send(It.IsAny<UpdatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdatePost(postId, request);

            // Assert
            Assert.Equal(200, result.Status);
            _senderMock.Verify(s => s.Send(It.Is<UpdatePostCommand>(c =>
                c.PostId == postId &&
                c.RequesterId == _testUserId 
            ), It.IsAny<CancellationToken>()), Times.Once);
        }


        [Fact]
        [Trait("Category", "UpdatePost - Failure")]
        public async Task UpdatePost_MissingAuthHeader_ReturnsUnauthorized()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var httpContextWithoutHeader = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext() { HttpContext = httpContextWithoutHeader };

            var request = new UpdatePostRequest("Title", "Summary", "Content", null, null);

            // Act
            var result = await _controller.UpdatePost(postId, request);

            // Assert
            Assert.Equal(401, result.Status);
            Assert.False(result.ResponseData);
            Assert.Contains("User not authenticated", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<UpdatePostCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        [Trait("Category", "UpdatePost - Failure")]
        public async Task UpdatePost_InvalidAuthHeader_ReturnsUnauthorized()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var httpContextInvalidHeader = new DefaultHttpContext();
            httpContextInvalidHeader.Request.Headers["X-Auth-Request-User"] = "not-a-guid";
            _controller.ControllerContext = new ControllerContext() { HttpContext = httpContextInvalidHeader };

            var request = new UpdatePostRequest("Title", "Summary", "Content", null, null);

            // Act
            var result = await _controller.UpdatePost(postId, request);

            // Assert
            Assert.Equal(401, result.Status);
            Assert.False(result.ResponseData);
            Assert.Contains("User not authenticated", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<UpdatePostCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // Bỏ test "WithEmptyNewFile" vì logic đó không còn ở Controller

        [Fact]
        [Trait("Category", "UpdatePost - Failure")]
        public async Task UpdatePost_WhenPostNotFound_HandlerReturnsNotFound()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var request = new UpdatePostRequest("Title", "Summary", "Content", null, null);
            var expectedResponse = new BaseResponseDto<bool> { Status = 404, Message = $"Post with ID {postId} not found.", ResponseData = false };

            _senderMock.Setup(s => s.Send(It.IsAny<UpdatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdatePost(postId, request);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.False(result.ResponseData);
            _senderMock.Verify(s => s.Send(It.Is<UpdatePostCommand>(c => c.PostId == postId && c.RequesterId == _testUserId), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("Category", "UpdatePost - Failure")]
        public async Task UpdatePost_WhenInvalidAttachmentIdSent_HandlerReturnsBadRequest()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var request = new UpdatePostRequest(
                Title: "Title",
                Summary: "Summary",
                Content: "Content",
                CategoryId: null,
                Tags: null
            );

            var expectedResponse = new BaseResponseDto<bool> { Status = 400, Message = "One or more new attachment IDs are invalid...", ResponseData = false };

            _senderMock.Setup(s => s.Send(It.IsAny<UpdatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdatePost(postId, request);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Equal(expectedResponse.Message, result.Message);
            _senderMock.Verify(s => s.Send(It.Is<UpdatePostCommand>(c => c.PostId == postId && c.RequesterId == _testUserId), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Bỏ test "WhenFileUploadFails" vì logic upload không còn ở đây

        [Fact]
        [Trait("Category", "UpdatePost - Exception")]
        public async Task UpdatePost_SenderThrowsException_ExceptionIsPropagated()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var request = new UpdatePostRequest("Title", "Summary", "Content", null, null);

            _senderMock.Setup(s => s.Send(It.IsAny<UpdatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ThrowsAsync(new InvalidOperationException("Database is offline"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.UpdatePost(postId, request));
            _senderMock.Verify(s => s.Send(It.Is<UpdatePostCommand>(c => c.PostId == postId && c.RequesterId == _testUserId), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("Category", "UpdatePost - Mapping")]
        public async Task UpdatePost_ShouldMapAllDataToCommandCorrectly()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var idsToDelete = new List<Guid> { Guid.NewGuid() };
            var idsToLink = new List<Guid> { Guid.NewGuid() };
            var tags = new List<string> { "tag1", "tag2" };

            var request = new UpdatePostRequest(
                Title: "Updated Title",
                Summary: "Updated Summary",
                Content: "Updated Content",
                CategoryId: categoryId,
                Tags: tags
            );

            UpdatePostCommand capturedCommand = null;
            _senderMock.Setup(s => s.Send(It.IsAny<UpdatePostCommand>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<bool>>, CancellationToken>((cmd, token) => capturedCommand = cmd as UpdatePostCommand)
                       .ReturnsAsync(new BaseResponseDto<bool> { Status = 200, ResponseData = true });

            // Act
            await _controller.UpdatePost(postId, request);

            // Assert
            Assert.NotNull(capturedCommand);
            Assert.Equal(postId, capturedCommand.PostId);
            Assert.Equal(_testUserId, capturedCommand.RequesterId);
            Assert.Equal(request.Title, capturedCommand.Title);
            Assert.Equal(request.Summary, capturedCommand.Summary);
            Assert.Equal(request.Content, capturedCommand.Content);
            Assert.Equal(request.CategoryId, capturedCommand.CategoryId);
            Assert.Equal(request.Tags, capturedCommand.Tags);
        }
    }
}