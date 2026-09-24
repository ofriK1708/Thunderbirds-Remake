using NUnit.Framework;
using Thunderbirds.Rules;

namespace Thunderbirds.Tests.EditMode
{
    public class AssemblySetupTests
    {
        [Test]
        public void RulesAssembly_DoesNotReferenceUnityEngine()
        {
            var referenced = typeof(ISimulation).Assembly.GetReferencedAssemblies();
            foreach (var name in referenced)
                StringAssert.DoesNotStartWith("UnityEngine", name.Name,
                    "Rules must stay Unity-free (COLLABORATION.md R4)");
        }
    }
}
