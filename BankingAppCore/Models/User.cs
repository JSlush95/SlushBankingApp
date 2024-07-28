using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace BankingAppCore.Models
{
    [Table("Users")]
    public class User : IdentityUser<int>
    {
        [MaxLength(256)]
        public string FirstName { get; set; }

        [MaxLength(256)]
        public string LastName { get; set; }

        [MaxLength(256)]
        public string Alias { get; set; }

        [Required]
        public UserType UserType { get; set; }

        [Required]
        public DateTime JoinDate { get; set; }

        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<User> manager)
        {
            var userIdentity = new ClaimsIdentity(await manager.GetClaimsAsync(this), "ApplicationCookie");
            return userIdentity;
        }
    }

    public enum UserType
    {
        Personal,
        Business
    }
}
