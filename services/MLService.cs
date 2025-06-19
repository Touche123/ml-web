using Microsoft.ML;
using Microsoft.ML.Data;
using ml_test.models;
using Emgu.CV;
using Emgu.CV.Structure;
using System.Drawing;
using System.Text.Json;

namespace ml_test.services
{
	public class MLService
	{
		private readonly MLContext _context = new();
		private PredictionEngine<ModelInput, ModelOutput>? _predEngine;
		public double Accuracy { get; private set; }
		public ConfusionMatrix? ConfusionMatrix { get; private set; }

		public void TrainImageModel(string rawJson)
		{
			//var rawJson = File.ReadAllText(jsonPath);
			//var dataList = JsonConvert.DeserializeObject<List<ModelInput>>(rawJson)!;
			//var data = _context.Data.LoadFromEnumerable(dataList);
			//var pipeline = _context.Transforms.Conversion.MapValueToKey("Label")
			//	.Append(_context.Transforms.Concatenate("Features", nameof(ModelInput.ImageFeatures)))
			//	.Append(_context.MulticlassClassification.Trainers.SdcaMaximumEntropy())
			//	.Append(_context.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

			//var split = _context.Data.TrainTestSplit(data, 0.2);
			//var model = pipeline.Fit(split.TrainSet);
			//var predictions = model.Transform(split.TestSet);
			//var metrics = _context.MulticlassClassification.Evaluate(predictions);
			//Accuracy = metrics.MicroAccuracy;
			//ConfusionMatrix = metrics.ConfusionMatrix;

			//_context.Model.Save(model, split.TrainSet.Schema, "imagemodel.zip");
			//_predEngine = _context.Model.CreatePredictionEngine<ModelInput, ModelOutput>(model);
			var dataList = JsonSerializer.Deserialize<List<ModelInput>>(rawJson)!;
			Console.WriteLine($"🔍 Rader: {dataList.Count}");

			if (dataList.Any(x => x.ImageFeatures == null || x.ImageFeatures.Length != 1024))
			{
				throw new Exception("🚨 Fel: minst en feature-array är null eller har fel längd.");
			}

			var data = _context.Data.LoadFromEnumerable(dataList);
			Console.WriteLine("✅ Data laddad, startar pipeline...");

			var pipeline = _context.Transforms.Conversion.MapValueToKey("Label")
				.Append(_context.Transforms.Concatenate("Features", nameof(ModelInput.ImageFeatures)))
				//.Append(_context.MulticlassClassification.Trainers.SdcaMaximumEntropy())
				.Append(_context.MulticlassClassification.Trainers.LbfgsMaximumEntropy())
				.Append(_context.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

			var split = _context.Data.TrainTestSplit(data, 0.2);
			Console.WriteLine("🚀 Kör Fit()");
			var model = pipeline.Fit(split.TrainSet);
			Console.WriteLine("🏁 Träning klar!");

			var predictions = model.Transform(split.TestSet);
			var metrics = _context.MulticlassClassification.Evaluate(predictions);
			Accuracy = metrics.MicroAccuracy;
			ConfusionMatrix = metrics.ConfusionMatrix;

			_context.Model.Save(model, split.TrainSet.Schema, "imagemodel.zip");
			_predEngine = _context.Model.CreatePredictionEngine<ModelInput, ModelOutput>(model);
		}

		public ModelOutput PredictImageOnly(float[] imageFeatures)
		{
			if (_predEngine == null)
			{
				var model = _context.Model.Load("imagemodel.zip", out _);
				_predEngine = _context.Model.CreatePredictionEngine<ModelInput, ModelOutput>(model);
			}
			return _predEngine.Predict(new ModelInput { ImageFeatures = imageFeatures });
		}

		public float[] ExtractFeaturesFromImage(string imagePath)
		{
			using var img = new Image<Bgr, byte>(imagePath);
			var resized = img.Resize(64, 64, Emgu.CV.CvEnum.Inter.Linear);
			var gray = resized.Convert<Gray, byte>();

			var features = new List<float>();
			var width = gray.Width;
			var height = gray.Height;
			var data = gray.Data; // EmguCVs bilddata [y, x, kanal]

			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					features.Add(data[y, x, 0]);
				}
			}

			return features.Take(1024).ToArray();
		}
	}
}
