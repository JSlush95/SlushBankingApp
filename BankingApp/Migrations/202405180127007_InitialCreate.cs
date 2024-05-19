namespace BankingApp.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.BankAccounts",
                c => new
                    {
                        AccountID = c.Int(nullable: false, identity: true),
                        Balance = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Holder = c.Int(nullable: false),
                        DateOpened = c.DateTime(nullable: false),
                        AccountType = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.AccountID)
                .ForeignKey("dbo.Users", t => t.Holder, cascadeDelete: true)
                .Index(t => t.Holder);
            
            CreateTable(
                "dbo.Users",
                c => new
                    {
                        UserID = c.Int(nullable: false, identity: true),
                        FirstName = c.String(maxLength: 256),
                        LastName = c.String(maxLength: 256),
                        JoinDate = c.DateTime(nullable: false),
                        Email = c.String(maxLength: 256),
                        EmailConfirmed = c.Boolean(nullable: false),
                        PasswordHash = c.String(),
                        SecurityStamp = c.String(),
                        PhoneNumber = c.String(),
                        PhoneNumberConfirmed = c.Boolean(nullable: false),
                        TwoFactorEnabled = c.Boolean(nullable: false),
                        LockoutEndDateUtc = c.DateTime(),
                        LockoutEnabled = c.Boolean(nullable: false),
                        AccessFailedCount = c.Int(nullable: false),
                        UserName = c.String(nullable: false, maxLength: 256),
                    })
                .PrimaryKey(t => t.UserID)
                .Index(t => t.UserName, unique: true, name: "UserNameIndex");
            
            CreateTable(
                "dbo.UserClaims",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        UserId = c.Int(nullable: false),
                        ClaimType = c.String(),
                        ClaimValue = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Users", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.UserLogins",
                c => new
                    {
                        LoginProvider = c.String(nullable: false, maxLength: 128),
                        ProviderKey = c.String(nullable: false, maxLength: 128),
                        UserId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.LoginProvider, t.ProviderKey, t.UserId })
                .ForeignKey("dbo.Users", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.UserRoles",
                c => new
                    {
                        UserId = c.Int(nullable: false),
                        RoleId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.UserId, t.RoleId })
                .ForeignKey("dbo.Users", t => t.UserId, cascadeDelete: true)
                .ForeignKey("dbo.Roles", t => t.RoleId, cascadeDelete: true)
                .Index(t => t.UserId)
                .Index(t => t.RoleId);
            
            CreateTable(
                "dbo.Cards",
                c => new
                    {
                        CardID = c.Int(nullable: false, identity: true),
                        CardType = c.Int(nullable: false),
                        CardNumber = c.String(nullable: false, maxLength: 11),
                        AssociatedAccount = c.Int(nullable: false),
                        IssueDate = c.DateTime(nullable: false),
                        Active = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.CardID)
                .ForeignKey("dbo.BankAccounts", t => t.AssociatedAccount, cascadeDelete: true)
                .Index(t => t.AssociatedAccount);
            
            CreateTable(
                "dbo.Roles",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 256),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.Name, unique: true, name: "RoleNameIndex");
            
            CreateTable(
                "dbo.TransactionRecords",
                c => new
                    {
                        TransactionID = c.Int(nullable: false, identity: true),
                        Sender = c.Int(nullable: false),
                        Recipient = c.Int(nullable: false),
                        Amount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TimeExecuted = c.DateTime(nullable: false),
                        Pending = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.TransactionID)
                .ForeignKey("dbo.BankAccounts", t => t.Recipient)
                .ForeignKey("dbo.BankAccounts", t => t.Sender)
                .Index(t => t.Sender)
                .Index(t => t.Recipient);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.TransactionRecords", "Sender", "dbo.BankAccounts");
            DropForeignKey("dbo.TransactionRecords", "Recipient", "dbo.BankAccounts");
            DropForeignKey("dbo.UserRoles", "RoleId", "dbo.Roles");
            DropForeignKey("dbo.Cards", "AssociatedAccount", "dbo.BankAccounts");
            DropForeignKey("dbo.BankAccounts", "Holder", "dbo.Users");
            DropForeignKey("dbo.UserRoles", "UserId", "dbo.Users");
            DropForeignKey("dbo.UserLogins", "UserId", "dbo.Users");
            DropForeignKey("dbo.UserClaims", "UserId", "dbo.Users");
            DropIndex("dbo.TransactionRecords", new[] { "Recipient" });
            DropIndex("dbo.TransactionRecords", new[] { "Sender" });
            DropIndex("dbo.Roles", "RoleNameIndex");
            DropIndex("dbo.Cards", new[] { "AssociatedAccount" });
            DropIndex("dbo.UserRoles", new[] { "RoleId" });
            DropIndex("dbo.UserRoles", new[] { "UserId" });
            DropIndex("dbo.UserLogins", new[] { "UserId" });
            DropIndex("dbo.UserClaims", new[] { "UserId" });
            DropIndex("dbo.Users", "UserNameIndex");
            DropIndex("dbo.BankAccounts", new[] { "Holder" });
            DropTable("dbo.TransactionRecords");
            DropTable("dbo.Roles");
            DropTable("dbo.Cards");
            DropTable("dbo.UserRoles");
            DropTable("dbo.UserLogins");
            DropTable("dbo.UserClaims");
            DropTable("dbo.Users");
            DropTable("dbo.BankAccounts");
        }
    }
}
