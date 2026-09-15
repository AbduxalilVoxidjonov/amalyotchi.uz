using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Students;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Domain;

public sealed class TutorScopeTests
{
    private static readonly Guid Tutor = Guid.CreateVersion7();
    private static readonly Guid FacultyA = Guid.CreateVersion7();
    private static readonly Guid FacultyB = Guid.CreateVersion7();
    private static readonly Guid DeptA1 = Guid.CreateVersion7();
    private static readonly Guid DeptA2 = Guid.CreateVersion7();
    private static readonly Guid DirA11 = Guid.CreateVersion7();
    private static readonly Guid DirA12 = Guid.CreateVersion7();
    private static readonly Guid GroupA111 = Guid.CreateVersion7();
    private static readonly Guid GroupA112 = Guid.CreateVersion7();

    private static TutorScope Faculty(Guid faculty) => TutorScope.Create(Tutor, TutorScopeLevel.Faculty, faculty);
    private static TutorScope Department(Guid dept) => TutorScope.Create(Tutor, TutorScopeLevel.Department, FacultyA, dept);
    private static TutorScope Direction(Guid dir) => TutorScope.Create(Tutor, TutorScopeLevel.Direction, FacultyA, DeptA1, dir);
    private static TutorScope Group(Guid group) => TutorScope.Create(Tutor, TutorScopeLevel.Group, FacultyA, DeptA1, DirA11, group);

    [Fact]
    public void Create_GuruhDarajasi_HammaOtaIdlarToldiriladi_Faol()
    {
        var scope = Group(GroupA111);

        scope.TutorUserId.Should().Be(Tutor);
        scope.Level.Should().Be(TutorScopeLevel.Group);
        scope.FacultyId.Should().Be(FacultyA);
        scope.DepartmentId.Should().Be(DeptA1);
        scope.DirectionId.Should().Be(DirA11);
        scope.StudentGroupId.Should().Be(GroupA111);
        scope.NodeId.Should().Be(GroupA111);
        scope.IsActive.Should().BeTrue();
        scope.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_FakultetDarajasi_NodeIdFakultet()
    {
        var scope = Faculty(FacultyA);

        scope.NodeId.Should().Be(FacultyA);
        scope.DepartmentId.Should().BeNull();
        scope.DirectionId.Should().BeNull();
        scope.StudentGroupId.Should().BeNull();
    }

    [Fact]
    public void Create_TyutorYokiFakultetBosh_XatoBeradi()
    {
        var noTutor = () => TutorScope.Create(Guid.Empty, TutorScopeLevel.Faculty, FacultyA);
        var noFaculty = () => TutorScope.Create(Tutor, TutorScopeLevel.Faculty, Guid.Empty);
        var badLevel = () => TutorScope.Create(Tutor, (TutorScopeLevel)9, FacultyA);

        noTutor.Should().Throw<DomainException>();
        noFaculty.Should().Throw<DomainException>();
        badLevel.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_DarajagaMosBolmaganOtaIdlar_XatoBeradi()
    {
        var deptMissing = () => TutorScope.Create(Tutor, TutorScopeLevel.Department, FacultyA);
        var dirMissing = () => TutorScope.Create(Tutor, TutorScopeLevel.Direction, FacultyA, DeptA1);
        var groupMissing = () => TutorScope.Create(Tutor, TutorScopeLevel.Group, FacultyA, DeptA1, DirA11);
        var extraOnFaculty = () => TutorScope.Create(Tutor, TutorScopeLevel.Faculty, FacultyA, DeptA1);
        var extraOnDirection = () => TutorScope.Create(Tutor, TutorScopeLevel.Direction, FacultyA, DeptA1, DirA11, GroupA111);

        deptMissing.Should().Throw<DomainException>();
        dirMissing.Should().Throw<DomainException>();
        groupMissing.Should().Throw<DomainException>();
        extraOnFaculty.Should().Throw<DomainException>();
        extraOnDirection.Should().Throw<DomainException>();
    }

    [Fact]
    public void FaolHolat_DeactivateVaActivate()
    {
        var scope = Direction(DirA11);

        scope.Deactivate();
        scope.IsActive.Should().BeFalse();

        scope.Activate();
        scope.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Overlaps_Teng_True()
    {
        TutorScope.Overlaps(Group(GroupA111), Group(GroupA111)).Should().BeTrue();
        TutorScope.Overlaps(Faculty(FacultyA), Faculty(FacultyA)).Should().BeTrue();
    }

    [Fact]
    public void Overlaps_OtaBola_IkkiTomonlamaTrue()
    {
        TutorScope.Overlaps(Faculty(FacultyA), Group(GroupA111)).Should().BeTrue();
        TutorScope.Overlaps(Group(GroupA111), Faculty(FacultyA)).Should().BeTrue();
        TutorScope.Overlaps(Department(DeptA1), Direction(DirA11)).Should().BeTrue();
        TutorScope.Overlaps(Direction(DirA11), Group(GroupA112)).Should().BeTrue();
    }

    [Fact]
    public void Overlaps_Qoshni_False()
    {
        TutorScope.Overlaps(Department(DeptA1), Department(DeptA2)).Should().BeFalse();
        TutorScope.Overlaps(Direction(DirA11), Direction(DirA12)).Should().BeFalse();
        TutorScope.Overlaps(Group(GroupA111), Group(GroupA112)).Should().BeFalse();
        // Boshqa kafedra ko'lami — shu kafedradagi yo'nalish bilan kesishmaydi
        TutorScope.Overlaps(Department(DeptA2), Direction(DirA11)).Should().BeFalse();
    }

    [Fact]
    public void Overlaps_HarXilFakultet_False()
    {
        TutorScope.Overlaps(Faculty(FacultyB), Faculty(FacultyA)).Should().BeFalse();
        TutorScope.Overlaps(Faculty(FacultyB), Group(GroupA111)).Should().BeFalse();
    }

    [Fact]
    public void CoversGroup_DarajagaQarab()
    {
        Faculty(FacultyA).CoversGroup(FacultyA, DeptA2, DirA12, GroupA112).Should().BeTrue();
        Department(DeptA1).CoversGroup(FacultyA, DeptA1, DirA12, GroupA112).Should().BeTrue();
        Department(DeptA1).CoversGroup(FacultyA, DeptA2, DirA12, GroupA112).Should().BeFalse();
        Direction(DirA11).CoversGroup(FacultyA, DeptA1, DirA11, GroupA112).Should().BeTrue();
        Direction(DirA11).CoversGroup(FacultyA, DeptA1, DirA12, GroupA112).Should().BeFalse();
        Group(GroupA111).CoversGroup(FacultyA, DeptA1, DirA11, GroupA111).Should().BeTrue();
        Group(GroupA111).CoversGroup(FacultyA, DeptA1, DirA11, GroupA112).Should().BeFalse();
    }

    [Fact]
    public void Normalize_OtaTanlansa_BolalariTashlanadi_TartibSaqlanadi()
    {
        var group = Group(GroupA111);
        var dept2 = Department(DeptA2);
        var dept1 = Department(DeptA1);
        var dir = Direction(DirA12);

        var result = TutorScope.Normalize([group, dept2, dept1, dir]);

        result.Should().Equal(dept2, dept1);
    }

    [Fact]
    public void Normalize_Takrorlar_BittagaKeltiriladi()
    {
        var first = Direction(DirA11);
        var duplicate = Direction(DirA11);

        var result = TutorScope.Normalize([first, duplicate]);

        result.Should().ContainSingle().Which.Should().BeSameAs(first);
    }

    [Fact]
    public void Normalize_Fakultet_HammasiniYutadi()
    {
        var faculty = Faculty(FacultyA);

        var result = TutorScope.Normalize([Group(GroupA111), Direction(DirA11), faculty, Department(DeptA2)]);

        result.Should().ContainSingle().Which.Should().BeSameAs(faculty);
    }

    [Fact]
    public void Normalize_QoshnilarSaqlanadi()
    {
        var scopes = new[] { Direction(DirA11), Direction(DirA12), Department(DeptA2) };

        TutorScope.Normalize(scopes).Should().Equal(scopes);
        TutorScope.Normalize([]).Should().BeEmpty();
    }
}
