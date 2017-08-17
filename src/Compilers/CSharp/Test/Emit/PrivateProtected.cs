﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    [CompilerTrait(CompilerFeature.PrivateProtected)]
    public class PrivateProtected : CSharpTestBase
    {
        private static readonly string s_keyPairFile = SigningTestHelpers.KeyPairFile;
        private static readonly string s_publicKeyFile = SigningTestHelpers.PublicKeyFile;
        private static readonly ImmutableArray<byte> s_publicKey = SigningTestHelpers.PublicKey;
        private static readonly DesktopStrongNameProvider s_defaultProvider = new SigningTestHelpers.VirtualizedStrongNameProvider(ImmutableArray.Create<string>());

        [Fact]
        public void RejectIncompatibleModifiers()
        {
            string source =
@"public class Base
{
    private internal int Field1;
    internal private int Field2;
    private internal protected int Field3;
    internal protected private int Field4;
}
";
            CreateStandardCompilation(source, parseOptions: TestOptions.Regular7_2)
                .VerifyDiagnostics(
                // (3,26): error CS0107: More than one protection modifier
                //     private internal int Field1;
                Diagnostic(ErrorCode.ERR_BadMemberProtection, "Field1").WithLocation(3, 26),
                // (4,26): error CS0107: More than one protection modifier
                //     internal private int Field2;
                Diagnostic(ErrorCode.ERR_BadMemberProtection, "Field2").WithLocation(4, 26),
                // (5,36): error CS0107: More than one protection modifier
                //     private internal protected int Field3;
                Diagnostic(ErrorCode.ERR_BadMemberProtection, "Field3").WithLocation(5, 36),
                // (6,36): error CS0107: More than one protection modifier
                //     internal protected private int Field4;
                Diagnostic(ErrorCode.ERR_BadMemberProtection, "Field4").WithLocation(6, 36)
                );
        }

        [Fact]
        public void AccessibleWhereRequired_01()
        {
            string source =
@"public class Base
{
    private protected int Field1;
    protected private int Field2;
}

public class Derived : Base
{
    void M()
    {
        Field1 = 1;
        Field2 = 2;
    }
}
";
            CreateStandardCompilation(source, parseOptions: TestOptions.Regular7_2)
                .VerifyDiagnostics(
                );
        }

        [Fact]
        public void AccessibleWhereRequired_02()
        {
            string source1 =
@"[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(""WantsIVTAccess"")]
public class Base
{
    private protected int Field1;
    protected private int Field2;
}";
            var baseCompilation = CreateStandardCompilation(source1, parseOptions: TestOptions.Regular7_2, options: TestOptions.ReleaseDll.WithStrongNameProvider(s_defaultProvider));

            string source2 =
@"public class Derived : Base
{
    void M()
    {
        Field1 = 1;
        Field2 = 2;
    }
}
";
            CreateStandardCompilation(source2, parseOptions: TestOptions.Regular7_2,
                references: new[] { new CSharpCompilationReference(baseCompilation) },
                assemblyName: "WantsIVTAccessButCantHave",
                options: TestOptions.ReleaseDll.WithStrongNameProvider(s_defaultProvider))
            .VerifyDiagnostics(
                // (5,9): error CS0122: 'Base.Field1' is inaccessible due to its protection level
                //         Field1 = 1;
                Diagnostic(ErrorCode.ERR_BadAccess, "Field1").WithArguments("Base.Field1").WithLocation(5, 9),
                // (6,9): error CS0122: 'Base.Field2' is inaccessible due to its protection level
                //         Field2 = 2;
                Diagnostic(ErrorCode.ERR_BadAccess, "Field2").WithArguments("Base.Field2").WithLocation(6, 9)
                );
            CreateStandardCompilation(source2, parseOptions: TestOptions.Regular7_2,
                references: new[] { MetadataReference.CreateFromImage(baseCompilation.EmitToArray()) },
                assemblyName: "WantsIVTAccessButCantHave",
                options: TestOptions.ReleaseDll.WithStrongNameProvider(s_defaultProvider))
            .VerifyDiagnostics(
                // (5,9): error CS0122: 'Base.Field1' is inaccessible due to its protection level
                //         Field1 = 1;
                Diagnostic(ErrorCode.ERR_BadAccess, "Field1").WithArguments("Base.Field1").WithLocation(5, 9),
                // (6,9): error CS0122: 'Base.Field2' is inaccessible due to its protection level
                //         Field2 = 2;
                Diagnostic(ErrorCode.ERR_BadAccess, "Field2").WithArguments("Base.Field2").WithLocation(6, 9)
                );

            CreateStandardCompilation(source2, parseOptions: TestOptions.Regular7_2,
                references: new[] { new CSharpCompilationReference(baseCompilation) },
                assemblyName: "WantsIVTAccess",
                options: TestOptions.ReleaseDll.WithStrongNameProvider(s_defaultProvider))
                .VerifyDiagnostics(
                );
            CreateStandardCompilation(source2, parseOptions: TestOptions.Regular7_2,
                references: new[] { MetadataReference.CreateFromImage(baseCompilation.EmitToArray()) },
                assemblyName: "WantsIVTAccess",
                options: TestOptions.ReleaseDll.WithStrongNameProvider(s_defaultProvider))
                .VerifyDiagnostics(
                );
        }

        [Fact]
        public void NotAccessibleWhereRequired()
        {
            string source =
@"public class Base
{
    private protected int Field1;
    protected private int Field2;
}

public class Derived // : Base
{
    void M()
    {
        Base b = null;
        b.Field1 = 1;
        b.Field2 = 2;
    }
}
";
            CreateStandardCompilation(source, parseOptions: TestOptions.Regular7_2)
                .VerifyDiagnostics(
                // (12,11): error CS0122: 'Base.Field1' is inaccessible due to its protection level
                //         b.Field1 = 1;
                Diagnostic(ErrorCode.ERR_BadAccess, "Field1").WithArguments("Base.Field1").WithLocation(12, 11),
                // (13,11): error CS0122: 'Base.Field2' is inaccessible due to its protection level
                //         b.Field2 = 2;
                Diagnostic(ErrorCode.ERR_BadAccess, "Field2").WithArguments("Base.Field2").WithLocation(13, 11)
                );
        }

        [Fact]
        public void NotInStructOrNamespace()
        {
            string source =
@"protected private struct Struct
{
    private protected int Field1;
}
";
            CreateStandardCompilation(source, parseOptions: TestOptions.Regular7_2)
                .VerifyDiagnostics(
                // (1,18): error CS1527: Elements defined in a namespace cannot be explicitly declared as private, protected, protected internal, or private protected
                // protected private struct Struct
                Diagnostic(ErrorCode.ERR_NoNamespacePrivate, "Struct").WithLocation(1, 26),
                // (3,27): error CS0666: 'Struct.Field1': new protected member declared in struct
                //     private protected int Field1;
                Diagnostic(ErrorCode.ERR_ProtectedInStruct, "Field1").WithArguments("Struct.Field1").WithLocation(3, 27)
                );
        }

        [Fact]
        public void NotInStaticClass()
        {
            string source =
@"static class C
{
    static private protected int Field1 = 2;
}
";
            CreateStandardCompilation(source, parseOptions: TestOptions.Regular7_2)
                .VerifyDiagnostics(
                // (3,34): error CS1057: 'C.Field1': static classes cannot contain protected members
                //     static private protected int Field1 = 2;
                Diagnostic(ErrorCode.ERR_ProtectedInStatic, "Field1").WithArguments("C.Field1").WithLocation(3, 34)
                );
        }

        [Fact]
        public void NestedTypes()
        {
            string source =
@"class Outer
{
    private protected class Inner
    {
    }
}
class Derived : Outer
{
    public void M()
    {
        Outer.Inner x = null;
    }
}
class NotDerived
{
    public void M()
    {
        Outer.Inner x = null; // error: Outer.Inner not accessible
    }
}
struct Struct
{
    private protected class Inner // error: protected not allowed in struct
    {
    }
}
";
            CreateStandardCompilation(source, parseOptions: TestOptions.Regular7_2)
                .VerifyDiagnostics(
                // (23,29): error CS0666: 'Struct.Inner': new protected member declared in struct
                //     private protected class Inner // error: protected not allowed in struct
                Diagnostic(ErrorCode.ERR_ProtectedInStruct, "Inner").WithArguments("Struct.Inner").WithLocation(23, 29),
                // (11,21): warning CS0219: The variable 'x' is assigned but its value is never used
                //         Outer.Inner x = null;
                Diagnostic(ErrorCode.WRN_UnreferencedVarAssg, "x").WithArguments("x").WithLocation(11, 21),
                // (18,15): error CS0122: 'Outer.Inner' is inaccessible due to its protection level
                //         Outer.Inner x = null; // error: Outer.Inner not accessible
                Diagnostic(ErrorCode.ERR_BadAccess, "Inner").WithArguments("Outer.Inner").WithLocation(18, 15)
                );
        }

        [Fact]
        public void PermittedAccessorProtection()
        {
            string source =
@"class Class
{
    public int Prop1 { get; private protected set; }
    protected internal int Prop2 { get; private protected set; }
    protected int Prop3 { get; private protected set; }
    internal int Prop4 { get; private protected set; }
    private protected int Prop5 { get; private set; }
}";
            CreateStandardCompilation(source, parseOptions: TestOptions.Regular7_2)
                .VerifyDiagnostics(
                );
        }

        [Fact]
        public void ForbiddenAccessorProtection_01()
        {
            string source =
@"class Class
{
    private protected int Prop1 { get; private protected set; }
    private int Prop2 { get; private protected set; }
}";
            CreateStandardCompilation(source, parseOptions: TestOptions.Regular7_2)
                .VerifyDiagnostics(
                // (3,58): error CS0273: The accessibility modifier of the 'Class.Prop1.set' accessor must be more restrictive than the property or indexer 'Class.Prop1'
                //     private protected int Prop1 { get; private protected set; }
                Diagnostic(ErrorCode.ERR_InvalidPropertyAccessMod, "set").WithArguments("Class.Prop1.set", "Class.Prop1").WithLocation(3, 58),
                // (4,48): error CS0273: The accessibility modifier of the 'Class.Prop2.set' accessor must be more restrictive than the property or indexer 'Class.Prop2'
                //     private int Prop2 { get; private protected set; }
                Diagnostic(ErrorCode.ERR_InvalidPropertyAccessMod, "set").WithArguments("Class.Prop2.set", "Class.Prop2").WithLocation(4, 48)
                );
        }

        [Fact]
        public void ForbiddenAccessorProtection_02()
        {
            string source =
@"interface ISomething
{
    private protected int M();
}";
            CreateStandardCompilation(source, parseOptions: TestOptions.Regular7_2)
                .VerifyDiagnostics(
                // (3,27): error CS0106: The modifier 'private protected' is not valid for this item
                //     private protected int M();
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M").WithArguments("private protected").WithLocation(3, 27)
                );
        }

        [Fact]
        public void AtLeastAsRestrictivePositive_01()
        {
            string source =
@"
public class C
{
    internal class Internal {}
    protected class Protected {}
    private protected void M(Internal x) {} // ok
    private protected void M(Protected x) {} // ok
    private protected class Nested
    {
        public void M(Internal x) {} // ok
        public void M(Protected x) {} // ok
    }
}
";
            CreateStandardCompilation(source, parseOptions: TestOptions.Regular7_2)
                .VerifyDiagnostics(
                );
        }

        [Fact]
        public void AtLeastAsRestrictiveNegative_01()
        {
            string source =
@"
public class Container
{
    private protected class PrivateProtected {}
    internal void M1(PrivateProtected x) {} // error: conflicting access
    protected void M2(PrivateProtected x) {} // error: conflicting access
}
";
            CreateStandardCompilation(source, parseOptions: TestOptions.Regular7_2)
                .VerifyDiagnostics(
                // (6,20): error CS0051: Inconsistent accessibility: parameter type 'Container.PrivateProtected' is less accessible than method 'Container.M2(Container.PrivateProtected)'
                //     protected void M2(PrivateProtected x) {} // error: conflicting access
                Diagnostic(ErrorCode.ERR_BadVisParamType, "M2").WithArguments("Container.M2(Container.PrivateProtected)", "Container.PrivateProtected").WithLocation(6, 20),
                // (5,19): error CS0051: Inconsistent accessibility: parameter type 'Container.PrivateProtected' is less accessible than method 'Container.M1(Container.PrivateProtected)'
                //     internal void M1(PrivateProtected x) {} // error: conflicting access
                Diagnostic(ErrorCode.ERR_BadVisParamType, "M1").WithArguments("Container.M1(Container.PrivateProtected)", "Container.PrivateProtected").WithLocation(5, 19)
                );
        }

        [Fact]
        public void DuplicateAccessInBinder()
        {
            string source =
@"
public class Container
{
    private public int Field;                           // 1
    private public int Property { get; set; }           // 2
    private public int M() => 1;                        // 3
    private public class C {}                           // 4
    private public struct S {}                          // 5
    private public enum E {}                            // 6
    private public event System.Action V;               // 7
    private public interface I {}                       // 8
    private public int this[int index] => 1;            // 9
    void Q() { V.Invoke(); V = null; }
}
";
            CreateStandardCompilation(source, parseOptions: TestOptions.Regular7_2)
                .VerifyDiagnostics(
                // (7,26): error CS0107: More than one protection modifier
                //     private public class C {}                           // 4
                Diagnostic(ErrorCode.ERR_BadMemberProtection, "C").WithLocation(7, 26),
                // (8,27): error CS0107: More than one protection modifier
                //     private public struct S {}                          // 5
                Diagnostic(ErrorCode.ERR_BadMemberProtection, "S").WithLocation(8, 27),
                // (9,25): error CS0107: More than one protection modifier
                //     private public enum E {}                            // 6
                Diagnostic(ErrorCode.ERR_BadMemberProtection, "E").WithLocation(9, 25),
                // (11,30): error CS0107: More than one protection modifier
                //     private public interface I {}                       // 8
                Diagnostic(ErrorCode.ERR_BadMemberProtection, "I").WithLocation(11, 30),
                // (4,24): error CS0107: More than one protection modifier
                //     private public int Field;                           // 1
                Diagnostic(ErrorCode.ERR_BadMemberProtection, "Field").WithLocation(4, 24),
                // (5,24): error CS0107: More than one protection modifier
                //     private public int Property { get; set; }           // 2
                Diagnostic(ErrorCode.ERR_BadMemberProtection, "Property").WithLocation(5, 24),
                // (6,24): error CS0107: More than one protection modifier
                //     private public int M() => 1;                        // 3
                Diagnostic(ErrorCode.ERR_BadMemberProtection, "M").WithLocation(6, 24),
                // (10,40): error CS0107: More than one protection modifier
                //     private public event System.Action V;               // 7
                Diagnostic(ErrorCode.ERR_BadMemberProtection, "V").WithLocation(10, 40),
                // (12,24): error CS0107: More than one protection modifier
                //     private public int this[int index] => 1;            // 9
                Diagnostic(ErrorCode.ERR_BadMemberProtection, "this").WithLocation(12, 24),
                // (12,43): error CS0107: More than one protection modifier
                //     private public int this[int index] => 1;            // 9
                Diagnostic(ErrorCode.ERR_BadMemberProtection, "1").WithLocation(12, 43)
                );
        }

        [Fact]
        public void NotInVersion71()
        {
            string source =
@"
public class Container
{
    private protected int Field;                           // 1
    private protected int Property { get; set; }           // 2
    private protected int M() => 1;                        // 3
    private protected class C {}                           // 4
    private protected struct S {}                          // 5
    private protected enum E {}                            // 6
    private protected event System.Action V;               // 7
    private protected interface I {}                       // 8
    private protected int this[int index] => 1;            // 9
    void Q() { V.Invoke(); V = null; }
}
";
            CreateStandardCompilation(source, parseOptions: TestOptions.Regular7_1)
                .VerifyDiagnostics(
                // (7,29): error CS8302: Feature 'private protected' is not available in C# 7.1. Please use language version 7.2 or greater.
                //     private protected class C {}                           // 4
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_1, "C").WithArguments("private protected", "7.2").WithLocation(7, 29),
                // (8,30): error CS8302: Feature 'private protected' is not available in C# 7.1. Please use language version 7.2 or greater.
                //     private protected struct S {}                          // 5
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_1, "S").WithArguments("private protected", "7.2").WithLocation(8, 30),
                // (9,28): error CS8302: Feature 'private protected' is not available in C# 7.1. Please use language version 7.2 or greater.
                //     private protected enum E {}                            // 6
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_1, "E").WithArguments("private protected", "7.2").WithLocation(9, 28),
                // (11,33): error CS8302: Feature 'private protected' is not available in C# 7.1. Please use language version 7.2 or greater.
                //     private protected interface I {}                       // 8
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_1, "I").WithArguments("private protected", "7.2").WithLocation(11, 33),
                // (4,27): error CS8302: Feature 'private protected' is not available in C# 7.1. Please use language version 7.2 or greater.
                //     private protected int Field;                           // 1
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_1, "Field").WithArguments("private protected", "7.2").WithLocation(4, 27),
                // (5,27): error CS8302: Feature 'private protected' is not available in C# 7.1. Please use language version 7.2 or greater.
                //     private protected int Property { get; set; }           // 2
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_1, "Property").WithArguments("private protected", "7.2").WithLocation(5, 27),
                // (6,27): error CS8302: Feature 'private protected' is not available in C# 7.1. Please use language version 7.2 or greater.
                //     private protected int M() => 1;                        // 3
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_1, "M").WithArguments("private protected", "7.2").WithLocation(6, 27),
                // (10,43): error CS8302: Feature 'private protected' is not available in C# 7.1. Please use language version 7.2 or greater.
                //     private protected event System.Action V;               // 7
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_1, "V").WithArguments("private protected", "7.2").WithLocation(10, 43),
                // (12,27): error CS8302: Feature 'private protected' is not available in C# 7.1. Please use language version 7.2 or greater.
                //     private protected int this[int index] => 1;            // 9
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_1, "this").WithArguments("private protected", "7.2").WithLocation(12, 27)
                );
            CreateStandardCompilation(source, parseOptions: TestOptions.Regular7_2)
                .VerifyDiagnostics(
                );
        }
    }
}