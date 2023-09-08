using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

var dbContext = new CourseraDbContext();
IDataProvider dataProvider = new DatabaseDataProvider(dbContext);

string pins = Console.ReadLine();
int minimumCredit = int.Parse(Console.ReadLine());
string startDate = Console.ReadLine();
string endDate = Console.ReadLine();
string outputFormat = Console.ReadLine();
string outputPath = Console.ReadLine();

var students = dataProvider.GetStudents(pins, minimumCredit, startDate, endDate);

if (outputFormat.ToLower() == "csv")
{
    IOutputCreator csvOutputCreator = new CsvOutputCreator();
    csvOutputCreator.CreateOutput(students, outputPath);
}
else if (outputFormat.ToLower() == "html")
{
    IOutputCreator htmlOutputCreator = new HtmlOutputCreator();
    htmlOutputCreator.CreateOutput(students, outputPath);
}
else 
{
    IOutputCreator csvOutputCreator = new CsvOutputCreator();
    csvOutputCreator.CreateOutput(students, outputPath);

    IOutputCreator htmlOutputCreator = new HtmlOutputCreator();
    htmlOutputCreator.CreateOutput(students, outputPath);
}


public class Student
{
    [Key]
    public string Pin { get; set; }

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public List<StudentCourse> StudentsCourses { get; set; }
}

public class Instructor
{
    [Key]
    public int Id { get; set; }

    public string FirstName { get; set; }

    public string LastName { get; set; }
}

public class Course
{
    [Key]
    public int Id { get; set; }

    public string Name { get; set; }

    public int InstructorId { get; set; }

    [ForeignKey(nameof(InstructorId))]
    public Instructor Instructor { get; set; }

    public byte TotalTime { get; set; }

    public byte Credit { get; set; }

    public List<StudentCourse> StudentsCourses { get; set; }
}

[Table("students_courses_xref")]
public class StudentCourse
{
    public string StudentPin { get; set; }
    public int CourseId { get; set; }
    public DateTime? CompletionDate { get; set; }
    public Course Course { get; set; }
    public Student Student { get; set; }

}

public class CourseraDbContext : DbContext
{
    public CourseraDbContext()
    {
    }

    public CourseraDbContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<Student> Students { get; set; }
    public DbSet<Instructor> Instructors { get; set; }
    public DbSet<Course> Courses { get; set; }
    public DbSet<StudentCourse> StudentsCourses { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer("Server=.;Database=coursera;Integrated Security=True;Encrypt=False;");
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StudentCourse>()
            .HasKey(e => new { e.StudentPin, e.CourseId });

        // Student Configurations
        modelBuilder.Entity<Student>()
            .Property(e => e.FirstName).HasColumnName("first_name");
        modelBuilder.Entity<Student>()
         .Property(e => e.LastName).HasColumnName("last_name");

        // Course Configurations
        modelBuilder.Entity<Course>()
           .Property(e => e.InstructorId).HasColumnName("instructor_id");
        modelBuilder.Entity<Course>()
           .Property(e => e.TotalTime).HasColumnName("total_time");

        // Instructor Configurations
        modelBuilder.Entity<Instructor>()
           .Property(e => e.FirstName).HasColumnName("first_name");
        modelBuilder.Entity<Instructor>()
         .Property(e => e.LastName).HasColumnName("last_name");

        // Student course xref Configurations
        modelBuilder.Entity<StudentCourse>()
          .Property(e => e.StudentPin).HasColumnName("student_pin");
        modelBuilder.Entity<StudentCourse>()
          .Property(e => e.CourseId).HasColumnName("course_id");
        modelBuilder.Entity<StudentCourse>()
          .Property(e => e.CompletionDate).HasColumnName("completion_date");

        base.OnModelCreating(modelBuilder);
    }
}

public class CourseDTO
{
    public string Name { get; set; }
    public int TotalTime { get; set; }
    public int Credit { get; set; }
    public string InstructorName { get; set; }
}

public class StudentInfoDTO
{
    public string Name { get; set; }
    public int TotalCredit { get; set; }
    public List<CourseDTO> Courses { get; set; }
}

public interface IDataProvider
{
    List<StudentInfoDTO> GetStudents(string? pins, int minimumCredit, string startDate, string endDate);
}

public class DatabaseDataProvider : IDataProvider
{
    private readonly CourseraDbContext _dbContext;

    public DatabaseDataProvider(CourseraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public List<StudentInfoDTO> GetStudents(string? pins, int minimumCredit, string startDate, string endDate)
    {
        string[] requiredPins = null;

        if (!string.IsNullOrWhiteSpace(pins))
        {
            requiredPins = pins.Split(',').ToArray();
        }

        DateTime rangeStart;
        bool isStartValid = DateTime.TryParse(startDate, out rangeStart);

        DateTime rangeEnd;
        bool isEndValid = DateTime.TryParse(endDate, out rangeEnd);

        if (!isStartValid || !isEndValid)
        {
            return null;
        }

        var qualifiedStudents = new List<StudentInfoDTO>();

        using (_dbContext)
        {
            if (requiredPins != null)
            {
                qualifiedStudents = _dbContext.Students
                    .Where(s =>
                    requiredPins.Any(p => p == s.Pin)
                    && s.StudentsCourses
                        .Where(sc => sc.CompletionDate.HasValue && sc.CompletionDate.Value.Date >= rangeStart.Date && sc.CompletionDate.Value.Date <= rangeEnd.Date)
                        .Sum(sc => sc.Course.Credit) >= minimumCredit
                        )
                    .Select(s => new StudentInfoDTO
                    {
                        Name = $"{s.FirstName} {s.LastName}",
                        TotalCredit = s.StudentsCourses
                            .Where(sc => sc.CompletionDate.HasValue && sc.CompletionDate.Value.Date >= rangeStart.Date && sc.CompletionDate.Value.Date <= rangeEnd.Date)
                            .Sum(sc => sc.Course.Credit),
                        Courses = s.StudentsCourses
                            .Where(sc => sc.CompletionDate.HasValue && sc.CompletionDate.Value.Date >= rangeStart.Date && sc.CompletionDate.Value.Date <= rangeEnd.Date)
                            .Select(sc => new CourseDTO
                            {
                                InstructorName = $"{sc.Course.Instructor.FirstName} {sc.Course.Instructor.LastName}",
                                Credit = sc.Course.Credit,
                                Name = sc.Course.Name,
                                TotalTime = sc.Course.TotalTime
                            })
                            .ToList()
                    })
                    .ToList();
            }
            else
            {
                qualifiedStudents = _dbContext.Students
                   .Where(s => s.StudentsCourses
                       .Where(sc => sc.CompletionDate.HasValue && sc.CompletionDate.Value.Date >= rangeStart.Date && sc.CompletionDate.Value.Date <= rangeEnd.Date)
                       .Sum(sc => sc.Course.Credit) >= minimumCredit
                       )
                   .Select(s => new StudentInfoDTO
                   {
                       Name = $"{s.FirstName} {s.LastName}",
                       TotalCredit = s.StudentsCourses
                           .Where(sc => sc.CompletionDate.HasValue && sc.CompletionDate.Value.Date >= rangeStart.Date && sc.CompletionDate.Value.Date <= rangeEnd.Date)
                           .Sum(sc => sc.Course.Credit),
                       Courses = s.StudentsCourses
                           .Where(sc => sc.CompletionDate.HasValue && sc.CompletionDate.Value.Date >= rangeStart.Date && sc.CompletionDate.Value.Date <= rangeEnd.Date)
                           .Select(sc => new CourseDTO
                           {
                               InstructorName = $"{sc.Course.Instructor.FirstName} {sc.Course.Instructor.LastName}",
                               Credit = sc.Course.Credit,
                               Name = sc.Course.Name,
                               TotalTime = sc.Course.TotalTime
                           })
                           .ToList()
                   })
                   .ToList();
            }

            return qualifiedStudents;
        }
    }
}

public interface IOutputCreator
{
    void CreateOutput(List<StudentInfoDTO> studentInfo, string outputPath);
}

public class HtmlOutputCreator : IOutputCreator
{
    public void CreateOutput(List<StudentInfoDTO> studentInfo, string outputPath)
    {
        StringBuilder html = new StringBuilder();

        // Table start
        html.Append("<table>");

        // General table header
        html.Append("<thead><tr>");
        html.Append("<th>Student</th>");
        html.Append("<th>Total Credit</th>");
        html.Append("<th></th>");
        html.Append("<th></th>");
        html.Append("<th></th>");
        html.Append("</tr>");
        html.Append("<tr><th></th>");
        html.Append("<th>Course Name</th>");
        html.Append("<th>Time</th>");
        html.Append("<th>Credit</th>");
        html.Append("<th>Instructor</th>");
        html.Append("</tr></thead>");

        // Dynamic table body
        html.Append("<tbody>");
        foreach (var student in studentInfo)
        {
            // Generate student info
            html.Append("<tr>");
            html.Append($"<td>{student.Name}</td>");
            html.Append($"<td>{student.TotalCredit}</td>");
            html.Append("<td></td>");
            html.Append("<td></td>");
            html.Append("<td></td>");
            html.Append("</tr>");

            // Generate student courses info
            foreach (var course in student.Courses)
            {
                html.Append("<tr>");
                html.Append("<td></td>");
                html.Append($"<td>{course.Name}</td>");
                html.Append($"<td>{course.TotalTime}</td>");
                html.Append($"<td>{course.Credit}</td>");
                html.Append($"<td>{course.InstructorName}</td>");
                html.Append("</tr>");
            }
        }
        html.Append("</tbody>");

        // end the table
        html.Append("</table>");

        var htmlToString = html.ToString();

        string outputFile = $"{outputPath}/report.html";

        File.WriteAllText(outputFile, htmlToString);
    }
}

public class CsvOutputCreator : IOutputCreator
{
    public void CreateOutput(List<StudentInfoDTO> studentInfo, string outputPath)
    {
        using (StreamWriter sw = new StreamWriter($"{outputPath}/repost.csv"))
        {
            sw.WriteLine("Student,TotalCredit");
            sw.WriteLine("CourseName,Time,Credit,Instructor");

            foreach (var student in studentInfo)
            {
                sw.WriteLine($"{student.Name},{student.TotalCredit}");

                foreach (var course in student.Courses)
                {
                    sw.WriteLine($"{course.Name},{course.TotalTime},{course.Credit},{course.InstructorName}");
                }
            }
        }
    }
}

