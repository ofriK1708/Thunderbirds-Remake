using System.Runtime.CompilerServices;

// Tests may arrange state through the rules layer's internal setters; the Unity layer still can't.
[assembly: InternalsVisibleTo("Thunderbirds.Tests.EditMode")]
