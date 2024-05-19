namespace BankingApp.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddUniqueConstraintToCardNumber : DbMigration
    {
        public override void Up()
        {
            CreateIndex("dbo.Cards", "CardNumber", unique: true);
        }
        
        public override void Down()
        {
            DropIndex("dbo.Cards", new[] { "CardNumber" });
        }
    }
}
