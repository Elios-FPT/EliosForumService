using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Cache.Command;

namespace ForumService.Core.Handler.Cache.Command
{
    public class RemoveCacheCommandHandler : ICommandHandler<RemoveCacheCommand, BaseResponseDto<bool>>
    {
        private readonly IAppCacheService _appCacheService;

        public RemoveCacheCommandHandler(IAppCacheService appCacheService)
        {
            _appCacheService = appCacheService;
        }

        public async Task<BaseResponseDto<bool>> Handle(RemoveCacheCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Key.ToString()))
            {
                return new BaseResponseDto<bool>
                {
                    Status = 400,
                    Message = "Cache key cannot be null or empty.",
                    ResponseData = false
                };
            }

            try
            {
                await _appCacheService.RemoveAsync(request.Key.ToString());

                return new BaseResponseDto<bool>
                {
                    Status = 200,
                    Message = "Cache value removed successfully.",
                    ResponseData = true
                };
            }
            catch (Exception ex)
            {
                return new BaseResponseDto<bool>
                {
                    Status = 500,
                    Message = $"Failed to remove cache value: {ex.Message}",
                    ResponseData = false
                };
            }
        }
    }
}