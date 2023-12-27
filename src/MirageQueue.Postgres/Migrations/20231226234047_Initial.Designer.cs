﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MirageQueue.Postgres.Databases;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MirageQueue.Postgres.Migrations
{
    [DbContext(typeof(MirageQueueDbContext))]
    [Migration("20231226234047_Initial")]
    partial class Initial
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("MirageQueue.Messages.Entities.InboundMessage", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<DateTime>("CreateAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("MessageContract")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<DateTime?>("UpdateAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.ToTable("InboundMessage");
                });

            modelBuilder.Entity("MirageQueue.Messages.Entities.OutboundMessage", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("ConsumerEndpoint")
                        .IsRequired()
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<DateTime>("CreateAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("InboundMessageId")
                        .HasColumnType("uuid");

                    b.Property<string>("MessageContract")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<DateTime?>("UpdateAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("InboundMessageId");

                    b.ToTable("OutboundMessage");
                });

            modelBuilder.Entity("MirageQueue.Messages.Entities.ScheduledInboundMessage", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<DateTime>("CreateAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("ExecuteAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("MessageContract")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<DateTime?>("UpdateAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.ToTable("ScheduledInboundMessage");
                });

            modelBuilder.Entity("MirageQueue.Messages.Entities.OutboundMessage", b =>
                {
                    b.HasOne("MirageQueue.Messages.Entities.InboundMessage", "InboundMessage")
                        .WithMany()
                        .HasForeignKey("InboundMessageId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("InboundMessage");
                });
#pragma warning restore 612, 618
        }
    }
}
