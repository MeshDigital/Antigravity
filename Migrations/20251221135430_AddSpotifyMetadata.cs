using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SLSKDONET.Migrations
{
    /// <inheritdoc />
    public partial class AddSpotifyMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AnalysisOffset",
                table: "Tracks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AudioFingerprint",
                table: "Tracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BPM",
                table: "Tracks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Bitrate",
                table: "Tracks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BitrateScore",
                table: "Tracks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CuePointsJson",
                table: "Tracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrequencyCutoff",
                table: "Tracks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnriched",
                table: "Tracks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTrustworthy",
                table: "Tracks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusicalKey",
                table: "Tracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "QualityConfidence",
                table: "Tracks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QualityDetails",
                table: "Tracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpectralHash",
                table: "Tracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrequencyCutoff",
                table: "PlaylistTracks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnriched",
                table: "PlaylistTracks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTrustworthy",
                table: "PlaylistTracks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "QualityConfidence",
                table: "PlaylistTracks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QualityDetails",
                table: "PlaylistTracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpectralHash",
                table: "PlaylistTracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AlbumArtUrl",
                table: "PlaylistJobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MissingCount",
                table: "PlaylistJobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "PlaylistJobs",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnalysisOffset",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "AudioFingerprint",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "BPM",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "Bitrate",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "BitrateScore",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "CuePointsJson",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "FrequencyCutoff",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "IsEnriched",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "IsTrustworthy",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "MusicalKey",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "QualityConfidence",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "QualityDetails",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "SpectralHash",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "FrequencyCutoff",
                table: "PlaylistTracks");

            migrationBuilder.DropColumn(
                name: "IsEnriched",
                table: "PlaylistTracks");

            migrationBuilder.DropColumn(
                name: "IsTrustworthy",
                table: "PlaylistTracks");

            migrationBuilder.DropColumn(
                name: "QualityConfidence",
                table: "PlaylistTracks");

            migrationBuilder.DropColumn(
                name: "QualityDetails",
                table: "PlaylistTracks");

            migrationBuilder.DropColumn(
                name: "SpectralHash",
                table: "PlaylistTracks");

            migrationBuilder.DropColumn(
                name: "AlbumArtUrl",
                table: "PlaylistJobs");

            migrationBuilder.DropColumn(
                name: "MissingCount",
                table: "PlaylistJobs");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "PlaylistJobs");
        }
    }
}
