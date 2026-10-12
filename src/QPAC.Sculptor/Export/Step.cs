using System.IO;
using System.Text;

namespace QPAC.Sculptor
{
    /// <summary>Writes a <see cref="StepFile"/> as an ISO 10303-21 (AP214) STEP file.</summary>
    public class Step
    {
        /// <summary>Writes in the model's own units (<see cref="StepFile.Units"/>, inches by default).</summary>
        public static void WriteStep(string path, StepFile file, string productName)
            => WriteStep(path, file, productName, StepWriteOptions.Default);

        public static void WriteStep(string path, StepFile file, string productName, StepWriteOptions options)
            => File.WriteAllText(path, ToStepString(file, productName, options), new UTF8Encoding(false));

        public static void WriteStep(Stream stream, StepFile file, string productName, StepWriteOptions options = null)
        {
            var bytes = Encoding.ASCII.GetBytes(ToStepString(file, productName, options));
            stream.Write(bytes, 0, bytes.Length);
        }

        public static string ToStepString(StepFile file, string productName, StepWriteOptions options = null)
            => new StepWriter().Write(file, productName, options);
    }
}
