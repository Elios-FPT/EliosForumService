using Asp.Versioning;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects;
using ForumService.Contract.TransferObjects.Post;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static ForumService.Contract.UseCases.Post.Command;
using static ForumService.Contract.UseCases.Post.Query;
using static ForumService.Contract.UseCases.Post.Request;

namespace ForumService.Web.Controllers.Post
{
    /// <summary>
    /// Post management endpoints.
    /// </summary>
    [ApiVersion(1)]
    [Produces("application/json")]
    [ControllerName("Post")]
    [Route("api/v1/[controller]")]
    public class PostController : ControllerBase
    {
        protected readonly ISender _sender;

        public PostController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>
        /// Creates a new post with optional file attachments.
        /// </summary>
        /// <remarks>
        /// <pre>
        /// Description:
        /// This endpoint allows creating a new post. It accepts post data via form fields
        /// and any accompanying files as part of a multipart/form-data request.
        /// </pre>
        /// </remarks>
        /// <param name="request">A <see cref="CreatePostRequest"/> object containing the properties for the new post, sent as form data.</param>
        /// <param name="files">A list of files to be attached to the post.</param>
        /// <returns>
        /// → <seealso cref="CreatePostCommand" /><br/>
        /// → <seealso cref="CreatePostCommandHandler" /><br/>
        /// → A boolean indicating success.<br/>
        /// </returns>
        [HttpPost]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        // ... các response type khác
        public async Task<BaseResponseDto<bool>> CreatePost(
            // THAY ĐỔI 1: Sử dụng [FromForm] để nhận dữ liệu từ form-data
            [FromForm] CreatePostRequest request,

            // THAY ĐỔI 2: Nhận danh sách file dưới dạng một tham số riêng
            List<IFormFile> files)
        {
            // THAY ĐỔI 3: Logic mới để chuyển đổi IFormFile sang DTO
            var filesToUpload = new List<FileToUploadDto>();
            if (files is not null)
            {
                foreach (var file in files)
                {
                    if (file.Length > 0)
                    {
                        using var memoryStream = new MemoryStream();
                        await file.CopyToAsync(memoryStream);

                        filesToUpload.Add(new FileToUploadDto
                        {
                            FileName = file.FileName,
                            ContentType = file.ContentType,
                            Content = memoryStream.ToArray()
                        });
                    }
                }
            }

            // THAY ĐỔI 4: Tạo command với cấu trúc mới
            var command = new CreatePostCommand(
                AuthorId: request.AuthorId,
                CategoryId: request.CategoryId,
                Title: request.Title,
                Summary: request.Summary,
                Content: request.Content,
                PostType: request.PostType,
                FilesToUpload: filesToUpload // Gửi dữ liệu file thô đến handler
            );

            return await _sender.Send(command);
        }


        /// <summary>
        /// Updates an existing post and its file attachments.
        /// </summary>
        /// <remarks>
        /// This endpoint handles updating a post's text content, adding new files, and deleting existing files.
        /// It uses a multipart/form-data request.
        /// </remarks>
        /// <param name="postId">The ID of the post to update.</param>
        /// <param name="request">The post's updated metadata, sent as form data. Include 'attachmentIdsToDelete' to remove existing files.</param>
        /// <param name="files">A list of NEW files to be attached to the post.</param>
        /// <returns>A boolean indicating success.</returns>
        [HttpPut("{postId}")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<BaseResponseDto<bool>> UpdatePost(
            [FromRoute] Guid postId,
            [FromForm] UpdatePostRequest request,
            List<IFormFile> files)
        {
            // Chuyển đổi các file mới (nếu có) sang DTO
            var newFilesToUpload = new List<FileToUploadDto>();
            if (files is not null)
            {
                foreach (var file in files)
                {
                    if (file.Length > 0)
                    {
                        using var memoryStream = new MemoryStream();
                        await file.CopyToAsync(memoryStream);

                        newFilesToUpload.Add(new FileToUploadDto
                        {
                            FileName = file.FileName,
                            ContentType = file.ContentType,
                            Content = memoryStream.ToArray()
                        });
                    }
                }
            }

            // Tạo command với cấu trúc mới, bao gồm cả file mới và file cần xóa
            var command = new UpdatePostCommand(
                PostId: postId,
                Title: request.Title,
                Summary: request.Summary,
                Content: request.Content,
                CategoryId: request.CategoryId,
                NewFilesToUpload: newFilesToUpload,
                AttachmentIdsToDelete: request.AttachmentIdsToDelete
            );

            return await _sender.Send(command);
        }

        /// <summary>
        /// Retrieves a paginated list of posts with optional filters.
        /// </summary>
        [HttpGet]
       [ProducesResponseType(typeof(BaseResponseDto<IEnumerable<PostViewDto>>), StatusCodes.Status200OK)]
       [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
       public async Task<BaseResponseDto<IEnumerable<PostViewDto>>> GetPublicViewPosts([FromQuery] GetPublicViewPostsRequest request)
       {
            try
            {
                // Ánh xạ toàn bộ các trường từ request sang query
                var query = new GetPublicViewPostsQuery(
                    AuthorId: request.AuthorId,
                    CategoryId: request.CategoryId,
                    SearchKeyword: request.SearchKeyword,
                    Limit: request.Limit,
                    Offset: request.Offset,
                    // --- Ánh xạ các trường Sort ---
                    Tags: request.Tags,
                    SortBy: request.SortBy,
                    SortOrder: request.SortOrder
                );

                return await _sender.Send(query);
            }
            catch (Exception ex)
            {
                // Phần xử lý lỗi vẫn giữ nguyên
                return new BaseResponseDto<IEnumerable<PostViewDto>>
                {
                    Status = 500,
                    Message = $"Failed to retrieve posts: {ex.Message}",
                    ResponseData = Enumerable.Empty<PostViewDto>()
                };
            }
        }
       /*

       /// <summary>
       /// Retrieves a single post by its ID.
       /// </summary>
       [HttpGet("{postId}")]
       [ProducesResponseType(typeof(BaseResponseDto<PostDto>), StatusCodes.Status200OK)]
       [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
       public async Task<BaseResponseDto<PostDto>> GetPostById([FromRoute] GetPostByIdRequest request)
       {
           var query = new GetPostByIdQuery(PostId: request.PostId);
           return await Sender.Send(query);
       }

        /// <summary>
       /// Deletes a post by its ID.
       /// </summary>
       [HttpDelete("{postId}")]
       [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
       [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
       [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
       [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
       [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
       public async Task<BaseResponseDto<bool>> DeletePost([FromRoute] DeletePostRequest request)
       {
           var command = new DeletePostCommand(PostId: request.PostId);
           return await Sender.Send(command);
       }

       /// <summary>
       /// Increments the view count of a post.
       /// </summary>
       [HttpPut("{postId}/view")]
       [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
       [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
       public async Task<BaseResponseDto<bool>> IncrementViewCount([FromRoute] IncrementViewCountRequest request)
       {
           var command = new IncrementViewCountCommand(PostId: request.PostId);
           return await Sender.Send(command);
       }

       /// <summary>
       /// Marks or unmarks a post as featured.
       /// </summary>
       [HttpPut("{postId}/feature")]
       [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
       [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
       public async Task<BaseResponseDto<bool>> ToggleFeatured([FromRoute] Guid postId, [FromBody] ToggleFeaturedRequest request)
       {
           var command = new ToggleFeaturedCommand(PostId: postId, IsFeatured: request.IsFeatured);
           return await Sender.Send(command);
       }

       /// <summary>
       /// Retrieves the total number of posts by a specific author.
       /// </summary>
       [HttpGet("count/by-author")]
       [ProducesResponseType(typeof(BaseResponseDto<int>), StatusCodes.Status200OK)]
       [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
       public async Task<BaseResponseDto<int>> GetPostCountByAuthor([FromQuery] GetPostCountByAuthorRequest request)
       {
           var query = new GetPostCountByAuthorQuery(AuthorId: request.AuthorId);
           return await Sender.Send(query);
       }
       */
    }
}
