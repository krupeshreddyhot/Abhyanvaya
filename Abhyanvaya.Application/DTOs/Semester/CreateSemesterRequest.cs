using Abhyanvaya.Application.DTOs.Course;
using Abhyanvaya.Application.DTOs.Group;

namespace Abhyanvaya.Application.DTOs.Semester
{
    public class CreateSemesterRequest
    {
        public int Number { get; set; }        // 1,2,3...
        public string Name { get; set; }      // Semester 1
        public int CourseId { get; set; }       
        public int? GroupId { get; set; }
       
    }
}
