using NUnit.Framework;
using RazorEngine;

namespace Test.RazorEngine
{
    /// <summary>
    /// Various general tests.
    /// </summary>
    [TestFixture]
    public class VariousTestsFixture
    {
        /// <summary>
        /// Test if we can call GetTypes on the RazorEngine assembly.
        /// This will make sure all SecurityCritical attributes are valid.
        /// </summary>
        [Test]
        public void AssemblyIsScannable()
        {
            typeof(Engine).Assembly.GetTypes();
        }
        /*
        /// <summary>
        /// Check that Contracts are enabled and work on this build machine.
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void ConstractsWork()
        {
            System.Diagnostics.Contracts.Contract.Requires<ArgumentException>(false);
        }

        [Test]
        //[ExpectedException(typeof(Exception))]
        public void ConstractsWork_2()
        {
            System.Diagnostics.Contracts.Contract.Requires(false);
        }*/
    }
}
