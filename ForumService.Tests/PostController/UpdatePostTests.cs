using ForumService.Contract.Shared;
using ForumService.Web.Controllers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc; 
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ForumService.Contract.UseCases.Post.Command;
using static ForumService.Contract.UseCases.Post.Request;

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

    
        private IFormFile CreateMockFormFile(string fileName, string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);
            return new FormFile(stream, 0, bytes.Length, "files", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain"
            };
        }

        [Fact]
        [Trait("Category", "UpdatePost - HappyPath")]
        public async Task UpdatePost_WithTextChangesOnly_ReturnsSuccess()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var request = new UpdatePostRequest("Updated Title", "Updated Summary", "Updated Content", Guid.NewGuid());
            var files = new List<IFormFile>();
            var expectedResponse = new BaseResponseDto<bool> { Status = 200, ResponseData = true };

            _senderMock.Setup(s => s.Send(It.IsAny<UpdatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdatePost(postId, request, files);

            // Assert
            Assert.Equal(200, result.Status);
            _senderMock.Verify(s => s.Send(It.Is<UpdatePostCommand>(c =>
                c.PostId == postId &&
                c.RequesterId == _testUserId && // Verify RequesterId
                c.NewFilesToUpload != null && c.NewFilesToUpload.Count == 0 &&
                (c.AttachmentIdsToDelete == null || c.AttachmentIdsToDelete.Count == 0)),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("Category", "UpdatePost - HappyPath")]
        public async Task UpdatePost_AddNewFiles_ReturnsSuccess()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var request = new UpdatePostRequest("Title", "Summary", "Content", null);
            var newFiles = new List<IFormFile> { CreateMockFormFile("new_file.txt", "new content") };
            var expectedResponse = new BaseResponseDto<bool> { Status = 200, ResponseData = true };

            _senderMock.Setup(s => s.Send(It.IsAny<UpdatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdatePost(postId, request, newFiles);

            // Assert
            Assert.Equal(200, result.Status);
            _senderMock.Verify(s => s.Send(It.Is<UpdatePostCommand>(c =>
                c.PostId == postId &&
                c.RequesterId == _testUserId && // Verify RequesterId
                c.NewFilesToUpload != null && c.NewFilesToUpload.Count == 1 && c.NewFilesToUpload.First().FileName == "new_file.txt"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("Category", "UpdatePost - HappyPath")]
        public async Task UpdatePost_DeleteExistingFiles_ReturnsSuccess()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var idsToDelete = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            var request = new UpdatePostRequest("Title", "Summary", "Content", null, AttachmentIdsToDelete: idsToDelete);
            var files = new List<IFormFile>();
            var expectedResponse = new BaseResponseDto<bool> { Status = 200, ResponseData = true };

            _senderMock.Setup(s => s.Send(It.IsAny<UpdatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdatePost(postId, request, files);

            // Assert
            Assert.Equal(200, result.Status);
            _senderMock.Verify(s => s.Send(It.Is<UpdatePostCommand>(c =>
                c.PostId == postId &&
                c.RequesterId == _testUserId && // Verify RequesterId
                c.AttachmentIdsToDelete != null && c.AttachmentIdsToDelete.Count == 2 && c.AttachmentIdsToDelete.SequenceEqual(idsToDelete)),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("Category", "UpdatePost - HappyPath")]
        public async Task UpdatePost_AddAndDeleteFiles_ReturnsSuccess()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var idsToDelete = new List<Guid> { Guid.NewGuid() };
            var request = new UpdatePostRequest("Title", "Summary", "Content", null, AttachmentIdsToDelete: idsToDelete);
            var newFiles = new List<IFormFile> { CreateMockFormFile("new.txt", "new") };
            var expectedResponse = new BaseResponseDto<bool> { Status = 200, ResponseData = true };

            _senderMock.Setup(s => s.Send(It.IsAny<UpdatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdatePost(postId, request, newFiles);

            // Assert
            Assert.Equal(200, result.Status);
            _senderMock.Verify(s => s.Send(It.Is<UpdatePostCommand>(c =>
                c.PostId == postId &&
                c.RequesterId == _testUserId && // Verify RequesterId
                c.NewFilesToUpload != null && c.NewFilesToUpload.Count == 1 &&
                c.AttachmentIdsToDelete != null && c.AttachmentIdsToDelete.Count == 1),
                It.IsAny<CancellationToken>()), Times.Once);
        }

    
        [Fact]
        [Trait("Category", "UpdatePost - Failure")]
        public async Task UpdatePost_MissingAuthHeader_ReturnsUnauthorized()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var httpContextWithoutHeader = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext() { HttpContext = httpContextWithoutHeader };

            var request = new UpdatePostRequest("Title", "Summary", "Content", null);
            var files = new List<IFormFile>();

            // Act
            var result = await _controller.UpdatePost(postId, request, files);

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

            var request = new UpdatePostRequest("Title", "Summary", "Content", null);
            var files = new List<IFormFile>();

            // Act
            var result = await _controller.UpdatePost(postId, request, files);

            // Assert
            Assert.Equal(401, result.Status);
            Assert.False(result.ResponseData);
            Assert.Contains("User not authenticated", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<UpdatePostCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }
   

        [Fact]
        [Trait("Category", "UpdatePost - EdgeCase")]
        public async Task UpdatePost_WithEmptyNewFile_IgnoresEmptyFile()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var request = new UpdatePostRequest("Title", "Summary", "Content", null);
            var emptyFile = new FormFile(new MemoryStream(), 0, 0, "files", "empty.txt");
            var newFiles = new List<IFormFile> { emptyFile, CreateMockFormFile("valid.txt", "valid") };
            var expectedResponse = new BaseResponseDto<bool> { Status = 200, ResponseData = true };

            _senderMock.Setup(s => s.Send(It.IsAny<UpdatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            await _controller.UpdatePost(postId, request, newFiles);

            // Assert
            _senderMock.Verify(s => s.Send(It.Is<UpdatePostCommand>(c =>
                c.PostId == postId &&
                c.RequesterId == _testUserId && // Verify RequesterId
                c.NewFilesToUpload != null && c.NewFilesToUpload.Count == 1 && 
                c.NewFilesToUpload.First().FileName == "valid.txt"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("Category", "UpdatePost - Failure")]
        public async Task UpdatePost_WhenPostNotFound_HandlerReturnsNotFound()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var request = new UpdatePostRequest("Title", "Summary", "Content", null);
            var files = new List<IFormFile>();
            var expectedResponse = new BaseResponseDto<bool> { Status = 404, Message = $"Post with ID {postId} not found.", ResponseData = false };

            _senderMock.Setup(s => s.Send(It.IsAny<UpdatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdatePost(postId, request, files);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.False(result.ResponseData);
            _senderMock.Verify(s => s.Send(It.Is<UpdatePostCommand>(c => c.PostId == postId && c.RequesterId == _testUserId), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("Category", "UpdatePost - Failure")]
        public async Task UpdatePost_WhenHandlerReturnsBadRequest_ControllerReturnsSame()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var request = new UpdatePostRequest("", "Summary", "Content", null); // Invalid Title
            var files = new List<IFormFile>();
            var expectedResponse = new BaseResponseDto<bool> { Status = 400, Message = "Title cannot be empty.", ResponseData = false };

            _senderMock.Setup(s => s.Send(It.IsAny<UpdatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdatePost(postId, request, files);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Equal("Title cannot be empty.", result.Message);
            _senderMock.Verify(s => s.Send(It.Is<UpdatePostCommand>(c => c.PostId == postId && c.RequesterId == _testUserId), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("Category", "UpdatePost - Failure")]
        public async Task UpdatePost_WhenFileUploadFails_HandlerReturnsInternalError()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var request = new UpdatePostRequest("Title", "Summary", "Content", null);
            var newFiles = new List<IFormFile> { CreateMockFormFile("fail.txt", "fail content") };
            var expectedResponse = new BaseResponseDto<bool> { Status = 500, Message = "Failed to upload new file: fail.txt. Update cancelled.", ResponseData = false };

            _senderMock.Setup(s => s.Send(It.IsAny<UpdatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdatePost(postId, request, newFiles);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Contains("Failed to upload new file", result.Message);
            _senderMock.Verify(s => s.Send(It.Is<UpdatePostCommand>(c => c.PostId == postId && c.RequesterId == _testUserId), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("Category", "UpdatePost - Exception")]
        public async Task UpdatePost_SenderThrowsException_ExceptionIsPropagated()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var request = new UpdatePostRequest("Title", "Summary", "Content", null);
            var files = new List<IFormFile>();

            _senderMock.Setup(s => s.Send(It.IsAny<UpdatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ThrowsAsync(new InvalidOperationException("Database is offline"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.UpdatePost(postId, request, files));
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
            var request = new UpdatePostRequest("Updated Title", "Updated Summary", "Updated Content", categoryId, idsToDelete);
            var newFile = CreateMockFormFile("new_image.png", "image data");
            var files = new List<IFormFile> { newFile };

            UpdatePostCommand capturedCommand = null;
            _senderMock.Setup(s => s.Send(It.IsAny<UpdatePostCommand>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<bool>>, CancellationToken>((cmd, token) => capturedCommand = cmd as UpdatePostCommand)
                       .ReturnsAsync(new BaseResponseDto<bool> { Status = 200, ResponseData = true });

            // Act
            await _controller.UpdatePost(postId, request, files);

            // Assert
            Assert.NotNull(capturedCommand);
            Assert.Equal(postId, capturedCommand.PostId);
            Assert.Equal(_testUserId, capturedCommand.RequesterId); // Verify RequesterId
            Assert.Equal(request.Title, capturedCommand.Title);
            Assert.Equal(request.Summary, capturedCommand.Summary);
            Assert.Equal(request.Content, capturedCommand.Content);
            Assert.Equal(request.CategoryId, capturedCommand.CategoryId);
            Assert.Equal(request.AttachmentIdsToDelete, capturedCommand.AttachmentIdsToDelete);
            Assert.NotNull(capturedCommand.NewFilesToUpload);
            Assert.Single(capturedCommand.NewFilesToUpload);
            Assert.Equal(newFile.FileName, capturedCommand.NewFilesToUpload[0].FileName);
            Assert.Equal(newFile.ContentType, capturedCommand.NewFilesToUpload[0].ContentType);
        }
    }
}
