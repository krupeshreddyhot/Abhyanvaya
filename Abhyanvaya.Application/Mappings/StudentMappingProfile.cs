using Abhyanvaya.Application.DTOs.Student;
using Abhyanvaya.Domain.Entities;
using AutoMapper;

namespace Abhyanvaya.Application.Mappings;

public sealed class StudentMappingProfile : Profile
{
    public StudentMappingProfile()
    {
        CreateMap<Student, StudentDto>()
            .ForMember(d => d.CourseName, o => o.MapFrom(s => s.Course != null ? s.Course.Name : ""))
            .ForMember(d => d.GroupName, o => o.MapFrom(s => s.Group != null ? s.Group.Name : ""))
            .ForMember(d => d.SemesterName, o => o.MapFrom(s => s.Semester != null ? s.Semester.Name : ""))
            .ForMember(d => d.GenderName, o => o.MapFrom(s => s.Gender != null ? s.Gender.Name : ""))
            .ForMember(d => d.MediumName, o => o.MapFrom(s => s.Medium != null ? s.Medium.Name : ""))
            .ForMember(d => d.FirstLanguageName, o => o.MapFrom(s => s.FirstLanguage != null ? s.FirstLanguage.Name : ""))
            .ForMember(d => d.LanguageName, o => o.MapFrom(s => s.Language != null ? s.Language.Name : ""));
    }
}
