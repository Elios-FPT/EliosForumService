using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Cache.Command;

namespace ForumService.Core.Handler.Cache.Command
{
    public class SetCacheCommandHandler : ICommandHandler<SetCacheCommand, BaseResponseDto<bool>>
    {
        private readonly IAppCacheService _appCacheService;

        public SetCacheCommandHandler(IAppCacheService appCacheService)
        {
            _appCacheService = appCacheService;
        }

        public async Task<BaseResponseDto<bool>> Handle(SetCacheCommand request, CancellationToken cancellationToken)
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

            if (request.Value == null)
            {
                return new BaseResponseDto<bool>
                {
                    Status = 400,
                    Message = "Cache value cannot be null.",
                    ResponseData = false
                };
            }

            try
            {
                await _appCacheService.SetAsync<string>(request.Key.ToString(), request.Value.ToString());

                return new BaseResponseDto<bool>
                {
                    Status = 200,
                    Message = "Cache value set successfully.",
                    ResponseData = true
                };
            }
            catch (Exception ex)
            {
                return new BaseResponseDto<bool>
                {
                    Status = 500,
                    Message = $"Failed to set cache value: {ex.Message}",
                    ResponseData = false
                };
            }
        }
    }
}