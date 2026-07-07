using System.Text.Json;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Models;

namespace Abhyanvaya.Application;

internal static class RecognitionSnapshotSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize(
        AttendanceSession session,
        AttendanceRecognition recognition,
        Student? student)
    {
        var snapshot = new RecognitionSnapshot
        {
            RecognitionId = recognition.Id,
            RecognitionStatus = recognition.RecognitionStatus,
            StudentId = recognition.StudentId,
            StudentName = student?.Name,
            ConfidenceScore = recognition.ConfidenceScore,
            EmbeddingDistance = recognition.EmbeddingDistance,
            BoundingBoxX = recognition.BoundingBoxX,
            BoundingBoxY = recognition.BoundingBoxY,
            BoundingBoxWidth = recognition.BoundingBoxWidth,
            BoundingBoxHeight = recognition.BoundingBoxHeight,
            RecognitionProvider = session.RecognitionProvider,
            RecognitionModel = session.RecognitionModel,
            RecognitionTimestamp = recognition.CreatedUtc
        };

        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }
}
