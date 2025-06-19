using Microsoft.ML.Data;

namespace ml_test.models
{
	public class ModelInput
	{
		[VectorType(1024)]
		public float[]? ImageFeatures { get; set; } // Bildbaserade features
		public string Label { get; set; } = string.Empty;
	}
}
