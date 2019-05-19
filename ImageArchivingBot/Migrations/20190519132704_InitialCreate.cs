using Microsoft.EntityFrameworkCore.Migrations;

namespace ImageArchivingBot.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Channels",
                columns: table => new
                {
                    Id = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(nullable: false),
                    GuildId = table.Column<ulong>(nullable: false),
                    IsCategory = table.Column<bool>(nullable: false),
                    Topic = table.Column<string>(nullable: false),
                    ParentId = table.Column<ulong>(nullable: true),
                    ChildIds = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Images",
                columns: table => new
                {
                    IdChecksumConcat = table.Column<string>(nullable: false),
                    Id = table.Column<ulong>(nullable: false),
                    Timestamp = table.Column<long>(nullable: false),
                    SenderId = table.Column<ulong>(nullable: false),
                    SenderUsername = table.Column<string>(nullable: false),
                    SenderDiscriminator = table.Column<string>(nullable: false),
                    ChannelId = table.Column<ulong>(nullable: false),
                    ChannelName = table.Column<string>(nullable: false),
                    MessageContent = table.Column<string>(nullable: false),
                    EditTimestamps = table.Column<string>(nullable: true),
                    EditContent = table.Column<string>(nullable: true),
                    Url = table.Column<string>(nullable: false),
                    FileName = table.Column<string>(nullable: false),
                    FileSize = table.Column<int>(nullable: false),
                    FileChecksum = table.Column<string>(nullable: false),
                    LocalFile = table.Column<string>(nullable: false),
                    ImageWidth = table.Column<int>(nullable: false),
                    ImageHeight = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Images", x => x.IdChecksumConcat);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    IdGuildConcat = table.Column<string>(nullable: false),
                    GuildId = table.Column<ulong>(nullable: false),
                    Id = table.Column<ulong>(nullable: false),
                    OptOut = table.Column<bool>(nullable: false),
                    Username = table.Column<string>(nullable: true),
                    Discriminator = table.Column<string>(nullable: true),
                    DisplayName = table.Column<string>(nullable: true),
                    Colour = table.Column<int>(nullable: true),
                    AvatarUri = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.IdGuildConcat);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Channels");

            migrationBuilder.DropTable(
                name: "Images");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
