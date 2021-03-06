﻿// <auto-generated />
using System;
using ImageArchivingBot;
using ImageArchivingBot.SupportLibs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ImageArchivingBot.Migrations
{
    [DbContext(typeof(DiscordDbContext))]
    partial class DiscordDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.2.4-servicing-10062");

            modelBuilder.Entity("ImageArchivingBot.Models.Channel", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("ChildIds");

                    b.Property<ulong>("GuildId");

                    b.Property<bool>("IsCategory");

                    b.Property<string>("Name")
                        .IsRequired();

                    b.Property<ulong?>("ParentId");

                    b.Property<string>("Topic")
                        .IsRequired();

                    b.HasKey("Id");

                    b.ToTable("Channels");
                });

            modelBuilder.Entity("ImageArchivingBot.Models.Image", b =>
                {
                    b.Property<string>("IdChecksumConcat")
                        .ValueGeneratedOnAdd();

                    b.Property<ulong>("ChannelId");

                    b.Property<string>("ChannelName")
                        .IsRequired();

                    b.Property<string>("EditContent");

                    b.Property<string>("EditTimestamps");

                    b.Property<string>("FileChecksum")
                        .IsRequired();

                    b.Property<string>("FileName")
                        .IsRequired();

                    b.Property<int>("FileSize");

                    b.Property<ulong>("Id");

                    b.Property<int>("ImageHeight");

                    b.Property<int>("ImageWidth");

                    b.Property<string>("LocalFile")
                        .IsRequired();

                    b.Property<string>("MessageContent")
                        .IsRequired();

                    b.Property<string>("SenderDiscriminator")
                        .IsRequired();

                    b.Property<ulong>("SenderId");

                    b.Property<string>("SenderUsername")
                        .IsRequired();

                    b.Property<long>("Timestamp");

                    b.Property<string>("Url")
                        .IsRequired();

                    b.HasKey("IdChecksumConcat");

                    b.ToTable("Images");
                });

            modelBuilder.Entity("ImageArchivingBot.Models.User", b =>
                {
                    b.Property<string>("IdGuildConcat")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("AvatarUri");

                    b.Property<int?>("Colour");

                    b.Property<string>("Discriminator");

                    b.Property<string>("DisplayName");

                    b.Property<ulong>("GuildId");

                    b.Property<ulong>("Id");

                    b.Property<bool>("OptOut");

                    b.Property<string>("Username");

                    b.HasKey("IdGuildConcat");

                    b.ToTable("Users");
                });
#pragma warning restore 612, 618
        }
    }
}
