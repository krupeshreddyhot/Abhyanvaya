namespace Abhyanvaya.Application.DTOs.Student
{
    public class UploadStudentsResultDto
    {
        public int Imported { get; set; }
        public int Skipped { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
