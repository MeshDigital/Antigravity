using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Models;

namespace SLSKDONET.Services;

/// <summary>
/// Service for generating high-fidelity waveform data (Peak + RMS) from audio files via FFmpeg.
/// Used for the "Max Ultra" waveform visualization.
/// </summary>
public class WaveformAnalysisService
{
    private readonly ILogger<WaveformAnalysisService> _logger;
    private readonly string _ffmpegPath = "ffmpeg"; // Assumes in PATH or co-located

    public WaveformAnalysisService(ILogger<WaveformAnalysisService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generates detailed waveform data for the given audio file.
    /// Spawns FFmpeg to decode to raw PCM and aggregates samples into Peak/RMS windows.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <param name="pointsPerSecond">Resolution of the waveform (default 100).</param>
    /// <returns>WaveformAnalysisData containing Peak and RMS arrays.</returns>
    public async Task<WaveformAnalysisData> GenerateWaveformAsync(string filePath, int pointsPerSecond = 100)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found", filePath);

        // FFmpeg command: Decode to raw s16le PCM, mono, 44100Hz
        // -f s16le: Signed 16-bit Little Endian
        // -ac 1: Downmix to mono (we only need one waveform for now)
        // -ar 44100: Standard sample rate
        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = $"-i \"{filePath}\" -f s16le -ac 1 -ar 44100 -vn -",
            RedirectStandardOutput = true,
            RedirectStandardError = true, // To avoid cluttering console
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var peakPoints = new List<byte>();
        var rmsPoints = new List<byte>();
        long totalSamples = 0;

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            // Fire and forget error reader to prevent buffer deadlocks
            _ = Task.Run(async () => 
            {
                try { await process.StandardError.ReadToEndAsync(); } catch { }
            });

            // Stream processing
            // 44100 Hz / 100 points = 441 samples per point
            int samplesPerPoint = 44100 / pointsPerSecond;
            // 16-bit sample = 2 bytes
            int bytesPerWindow = samplesPerPoint * 2;
            
            byte[] buffer = new byte[bytesPerWindow];
            var baseStream = process.StandardOutput.BaseStream;
            int bytesRead;

            while ((bytesRead = await baseStream.ReadAsync(buffer, 0, bytesPerWindow)) > 0)
            {
                // Process the window
                int sampleCount = bytesRead / 2;
                if (sampleCount == 0) continue;

                float maxPeak = 0;
                double sumSquares = 0;
                
                for (int i = 0; i < sampleCount; i++)
                {
                    // Read 16-bit sample
                    short sample = BitConverter.ToInt16(buffer, i * 2);
                    float normalized = Math.Abs(sample) / 32768f; // Normalize to 0.0 - 1.0

                    if (normalized > maxPeak) maxPeak = normalized;
                    sumSquares += normalized * normalized;
                }

                // Calculate metrics
                float rms = (float)Math.Sqrt(sumSquares / sampleCount);

                // Scale to byte (0-255)
                peakPoints.Add((byte)(Math.Clamp(maxPeak * 255, 0, 255)));
                rmsPoints.Add((byte)(Math.Clamp(rms * 255, 0, 255)));

                totalSamples += sampleCount;
            }

            await process.WaitForExitAsync();
            
            return new WaveformAnalysisData
            {
                PeakData = peakPoints.ToArray(),
                RmsData = rmsPoints.ToArray(),
                PointsPerSecond = pointsPerSecond,
                DurationSeconds = (double)totalSamples / 44100.0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate waveform for {File}", filePath);
            return new WaveformAnalysisData(); // Return empty on failure
        }
    }
}
