using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.TransferObjects.BanUser
{
    public class UserBanStatusDto
    {
        public bool IsBanned { get; set; }
        public string? Reason { get; set; }
        public DateTime? BanUntil { get; set; }
        public bool IsPermanent => IsBanned && !BanUntil.HasValue;
    }
}
