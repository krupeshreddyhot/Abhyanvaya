using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Application.Common.Interfaces
{
    public interface IApplicationDbContext
    {
        IQueryable<Student> Students { get; }
        IQueryable<Attendance> Attendances { get; }
        IQueryable<User> Users { get; }
        IQueryable<College> Colleges { get; }
        IQueryable<University> Universities { get; }
        IQueryable<Course> Courses { get; }
        IQueryable<Group> Groups { get; }
        IQueryable<Gender> Genders { get; }
        IQueryable<Medium> Mediums { get; }
        IQueryable<Language> Languages { get; }
        IQueryable<TenantSubject> TenantSubjects { get; }
        IQueryable<Subject> Subjects { get; }
        IQueryable<StudentSubject> StudentSubjects { get; }
        IQueryable<ElectiveGroup> ElectiveGroups { get; }
        IQueryable<Semester> Semesters { get; }
        Task AddAsync<T>(T entity) where T : class;
        void Remove<T>(T entity) where T : class;
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        void AddAttendances(IEnumerable<Attendance> attendances);
        Task AddRangeAsync<T>(IEnumerable<T> entities) where T : class;

    }
}
