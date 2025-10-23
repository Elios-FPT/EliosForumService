using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ForumService.Contract.Models
{
    public class User
    {
        public Guid id { get; set; }
        public string firstName { get; set; } = null!;
        public string lastName { get; set; } = null!;
        public DateTime dateOfBirth { get; set; }
        public string? gender { get; set; }
        public string? avatarUrl { get; set; }
        public string? avatarPrefix { get; set; }
        public string? avatarFileName { get; set; }
        public DateTime createdAt { get; set; }
        public DateTime updatedAt { get; set; }
    }

    public enum Gender
    {
        Male,
        Female,
        Other
    }
}
