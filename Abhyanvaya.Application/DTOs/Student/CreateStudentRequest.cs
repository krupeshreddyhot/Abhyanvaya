using Abhyanvaya.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Abhyanvaya.Application.DTOs.Student
{
    public class CreateStudentRequest
    {
        public required string StudentNumber { get; set; } // Unique
        public string? AppraId { get; set; }
        public required string Name { get; set; }

        public int CourseId { get; set; }
        public int GroupId { get; set; }
        public int GenderId { get; set; }
        public int MediumId { get; set; }

        /// <summary>Optional; defaults to English for the tenant when omitted.</summary>
        public int? FirstLanguageId { get; set; }

        public int LanguageId { get; set; }
        public int? Batch { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? MobileNumber { get; set; }
        public string? AlternateMobileNumber { get; set; }

        public string? Email { get; set; }

        public string? ParentMobileNumber { get; set; }
        public string? ParentAlternateMobileNumber { get; set; }

        public string? FatherName { get; set; }
        public string? MotherName { get; set; }   
        public int SemesterId { get; set; }
       
    }
}
