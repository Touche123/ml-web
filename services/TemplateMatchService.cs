using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System.Collections.Concurrent;
using System.Drawing;

namespace ml_test.services
{
	public class PointInfo
	{
		public Point Point { get; set; }
		public PointF Offset { get; set; }
		public PointF Derivative { get; set; }
		public double Magnitude { get; set; }

		public void Update(PointF center)
		{
			Offset = new PointF(Point.X - center.X, Point.Y - center.Y);
		}
	}

	public class TemplateModel
	{
		public int PyramidLevel { get; set; }
		public double Scale { get; set; }
		public double Rotation { get; set; }
		public List<PointInfo> Points { get; set; } = new();
		public Size BaseTemplateSize { get; set; }
	}

	public class MatchResult
	{
		public Point Position { get; set; }
		public double Score { get; set; }
		public double Scale { get; set; }
		public double Rotation { get; set; }
		public Size TemplateSize { get; set; }
	}

	public class TemplateMatchService
	{
		private readonly IWebHostEnvironment _env;
		private readonly List<TemplateModel> _models = new();
		private bool _trainingDone = false;

		public TemplateMatchService(IWebHostEnvironment env)
		{
			_env = env;
		}

		public void test()
		{
			string templatePath = "images/template3.png";
			string imagePath = "images/img1.png";

			if (!_trainingDone)
			{
				TrainTemplate(templatePath);
				_trainingDone = true;
			}
				
			var matches = MatchOptimizedWithROI(imagePath, minScore: 0.7);
			var imageFullPath = Path.Combine(_env.WebRootPath, imagePath);
			var img = new Image<Bgr, byte>(imageFullPath);

			foreach (var match in matches)
			{
				var topLeft = new Point(
					match.Position.X - match.TemplateSize.Width / 2,
					match.Position.Y - match.TemplateSize.Height / 2
				);
				var rect = new Rectangle(topLeft, match.TemplateSize);
				//var rect = new Rectangle(match.Position, match.TemplateSize);
				CvInvoke.Rectangle(img, rect, new MCvScalar(0, 0, 255), 2);
				CvInvoke.PutText(img, $"Score: {match.Score:F2}", new Point(rect.X, rect.Y - 10),
					FontFace.HersheySimplex, 0.5, new MCvScalar(0, 255, 0), 1);
			}

			// Steg 5: Visa resultatet
			CvInvoke.Imshow("Match Result", img);
			CvInvoke.WaitKey(0);
			CvInvoke.DestroyAllWindows();
		}

		/// <summary>
		/// Builds template models at multiple pyramid levels, rotations and scales.
		/// </summary>
		public void TrainTemplate(string templatePath)
		{
			var templateRaw = new Image<Gray, byte>(Path.Combine(_env.WebRootPath, templatePath));
			var pyramidLevels = 3;
			//double[] scales = { 0.8, 0.9, 1.0, 1.1, 1.2 };
			double[] scales = { 1.0 };
			//double[] rotations = { 0.0 };
			double[] rotations = Enumerable.Range(-5, 5).Select(i => (double)i).ToArray(); // -30 to +30 deg

			var src = templateRaw.Clone().Mat;

			for (int level = 0; level < pyramidLevels; level++)
			{
				foreach (var scale in scales)
				{
					foreach (var angle in rotations)
					{
						var scaled = new Mat();
						CvInvoke.Resize(src, scaled, Size.Empty, scale, scale, Inter.Linear);

						var center = new PointF(scaled.Width / 2f, scaled.Height / 2f);
						var rotMat = new Mat();
						CvInvoke.GetRotationMatrix2D(center, angle, 1.0, rotMat);
						var rotated = new Mat();
						CvInvoke.WarpAffine(scaled, rotated, rotMat, scaled.Size, Inter.Linear, Warp.Default, BorderType.Constant, new MCvScalar(0));
						
						var canny = new Mat();
						CvInvoke.Canny(rotated, canny, 100, 200);

						var gx = new Mat();
						var gy = new Mat();
						CvInvoke.Sobel(rotated, gx, DepthType.Cv64F, 1, 0, 3);
						CvInvoke.Sobel(rotated, gy, DepthType.Cv64F, 0, 1, 3);

						var magnitude = new Mat();
						var direction = new Mat();
						CvInvoke.CartToPolar(gx, gy, magnitude, direction);

						var imgGx = gx.ToImage<Gray, double>();
						var imgGy = gy.ToImage<Gray, double>();
						var imgMagnitude = magnitude.ToImage<Gray, double>();

						using var contours = new VectorOfVectorOfPoint();
						CvInvoke.FindContours(canny, contours, null, RetrType.List, ChainApproxMethod.ChainApproxNone);

						var pointInfos = new List<PointInfo>();
						var sum = new PointF(0, 0);

						for (int i = 0; i < contours.Size; i++)
						{
							var contour = contours[i].ToArray();
							foreach (var pt in contour)
							{
								double fdx = imgGx.GetDouble(pt.Y, pt.X);
								double fdy = imgGy.GetDouble(pt.Y, pt.X);
								double mag = imgMagnitude.GetDouble(pt.Y, pt.X);

								pointInfos.Add(new PointInfo
								{
									Point = pt,
									Derivative = new PointF((float)fdx, (float)fdy),
									Magnitude = mag == 0 ? 0 : 1 / mag
								});
								sum.X += pt.X;
								sum.Y += pt.Y;
							}
						}

						if (pointInfos.Count == 0)
							continue;

						var centerPt = new PointF(sum.X / pointInfos.Count, sum.Y / pointInfos.Count);
						foreach (var p in pointInfos)
							p.Update(centerPt);

						_models.Add(new TemplateModel
						{
							PyramidLevel = level,
							Scale = scale,
							Rotation = angle,
							Points = pointInfos,
							BaseTemplateSize = rotated.Size
						});
					}
				}

				CvInvoke.PyrDown(src, src);
			}

			int templateIndex = 0;

			foreach (var model in _models)
			{
				// Återskapa templatebilden med exakt samma transform pipeline som när modellen skapades
				var templateRaw2 = new Image<Gray, byte>(Path.Combine(_env.WebRootPath, templatePath));
				double pyramidScale = Math.Pow(0.5, model.PyramidLevel);

				double totalScale = model.Scale * pyramidScale;
				var scaled = templateRaw2.Resize(totalScale, Inter.Linear);
				var center = new PointF(scaled.Width / 2f, scaled.Height / 2f);

				var rotMat = new Mat();
				CvInvoke.GetRotationMatrix2D(center, model.Rotation, 1.0, rotMat);

				var rotated = new Image<Gray, byte>(scaled.Size);
				CvInvoke.WarpAffine(scaled, rotated, rotMat, scaled.Size, Inter.Linear, Warp.Default, BorderType.Constant, new MCvScalar(0));

				// Konvertera till Bgr för färgritning
				var colored = rotated.Convert<Bgr, byte>();

				// Rita ut konturerna baserat på transformerade punkter
				foreach (var p in model.Points)
				{
					// Eftersom Points är sparade med korrekt transform behöver du bara rita ut dem direkt
					var pt = p.Point;
					if (pt.X >= 0 && pt.X < colored.Width && pt.Y >= 0 && pt.Y < colored.Height)
					{
						CvInvoke.Circle(colored, pt, 1, new MCvScalar(0, 0, 255), -1); // Röd prick
					}
				}

				// Filnamn med info om pyramid, rotation och scale
				string fileName = $"template_{templateIndex}_pyr{model.PyramidLevel}_rot{model.Rotation}_scale{model.Scale:F2}.png";
				string outputPath = Path.Combine(_env.WebRootPath, "templates_debug", fileName);

				// Skapa mapp om ej finns
				Directory.CreateDirectory(Path.Combine(_env.WebRootPath, "templates_debug"));

				// Spara bilden
				colored.Save(outputPath);

				templateIndex++;
			}

			Console.WriteLine($"Template training complete: {_models.Count} models generated.");
		}

		public List<MatchResult> MatchOptimizedWithROI(string imagePath, double minScore = 0.7, int roiSize = 40, int topK = 5)
		{
			Console.WriteLine("Search started...");
			var results = new List<MatchResult>();
			var imageRaw = new Image<Gray, byte>(Path.Combine(_env.WebRootPath, imagePath));
			int pyramidLevels = 3;

			// Build image pyramid
			var imagePyramid = new List<Mat> { imageRaw.Mat.Clone() };
			for (int i = 1; i < pyramidLevels; i++)
			{
				var down = new Mat();
				CvInvoke.PyrDown(imagePyramid[i - 1], down);
				imagePyramid.Add(down);
			}

			// Step 1: Coarse search on lowest resolution
			int coarseLevel = pyramidLevels - 1;
			var coarseImg = imagePyramid[coarseLevel];

			var candidates = SearchLevel(coarseImg, coarseLevel, step: 4, 0.3);
			Console.WriteLine($"Coarse level {coarseLevel} found {candidates.Count} candidates");

			// Keep only topK candidates
			candidates = candidates.OrderByDescending(m => m.Score).Take(topK).ToList();

			// Step 2: Refine candidates through higher pyramid levels
			for (int level = coarseLevel - 1; level >= 0; level--)
			{
				var currentImg = imagePyramid[level];
				var refinedCandidates = new List<MatchResult>();

				int scaledRoiSize = Math.Max(roiSize, (int)(roiSize * Math.Pow(2, level)));

				foreach (var candidate in candidates)
				{
					// Scale up position
					int centerX = candidate.Position.X * 2;
					int centerY = candidate.Position.Y * 2;

					

					// Define ROI around candidate
					var roi = new Rectangle(centerX - scaledRoiSize / 2, centerY - scaledRoiSize / 2, scaledRoiSize, scaledRoiSize);
					roi.Intersect(new Rectangle(0, 0, currentImg.Width, currentImg.Height));

					if (roi.Width < 5 || roi.Height < 5)
						continue;

					var roiImg = new Mat(currentImg, roi);

					// Search in ROI with fine step=1, using only candidate's best rotation/scale combo
					var matchesInROI = SearchLevel(roiImg, level, step: 1, minScore, roi.Location, candidate.Rotation, candidate.Scale);

					refinedCandidates.AddRange(matchesInROI);
				}

				Console.WriteLine($"Level {level} refined to {refinedCandidates.Count} candidates");

				// Keep only topK for next refinement
				candidates = refinedCandidates.OrderByDescending(m => m.Score).Take(topK).ToList();
			}

			results.AddRange(candidates);
			Console.WriteLine($"Final results: {results.Count}");
			return results;
		}

		private List<MatchResult> SearchLevel(Mat img, int level, int step, double minScore, Point offset = default, double? fixedRotation = null, double? fixedScale = null)
		{
			var matches = new List<MatchResult>();

			var gx = new Mat();
			var gy = new Mat();
			CvInvoke.Sobel(img, gx, DepthType.Cv64F, 1, 0, 3);
			CvInvoke.Sobel(img, gy, DepthType.Cv64F, 0, 1, 3);

			var magnitude = new Mat();
			var direction = new Mat();
			CvInvoke.CartToPolar(gx, gy, magnitude, direction);

			var imgGx = gx.ToImage<Gray, double>();
			var imgGy = gy.ToImage<Gray, double>();
			var imgMagnitude = magnitude.ToImage<Gray, double>();

			var modelsToUse = _models.Where(m =>
				m.PyramidLevel == level &&
				(!fixedRotation.HasValue || m.Rotation == fixedRotation.Value) &&
				(!fixedScale.HasValue || m.Scale == fixedScale.Value)
			).ToList();

			Parallel.For(0, img.Rows, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, y =>
			{
				if (y % step != 0) return;

				for (int x = 0; x < img.Cols; x += step)
				{
					foreach (var model in modelsToUse)
					{
						double partialSum = 0;
						int count = 0;
						int totalPoints = model.Points.Count;
						double normMinScore = minScore / totalPoints;
						double greediness = 0.8;
						double normGreediness = (1 - greediness * minScore) / (1 - greediness) / totalPoints;

						foreach (var p in model.Points)
						{
							int curX = (int)(x + p.Offset.X);
							int curY = (int)(y + p.Offset.Y);

							if (curX < 0 || curY < 0 || curX >= img.Cols || curY >= img.Rows)
								continue;

							double iSx = imgGx.GetDouble(curY, curX);
							double iSy = imgGy.GetDouble(curY, curX);

							if ((iSx != 0 || iSy != 0) && (p.Derivative.X != 0 || p.Derivative.Y != 0))
							{
								double mag = imgMagnitude.GetDouble(curY, curX);
								double matGradMag = mag == 0 ? 0 : 1 / mag;

								//var angleTemplate = Math.Atan2(p.Derivative.Y, p.Derivative.X);
								//var angleImage = Math.Atan2(iSy, iSx);
								//var angleDiff = Math.Abs(angleTemplate - angleImage);
								//if (angleDiff > Math.PI) angleDiff = 2 * Math.PI - angleDiff;

								//partialSum += Math.Cos(angleDiff) * (p.Magnitude * matGradMag);
								//partialSum += ((iSx * p.Derivative.X) + (iSy * p.Derivative.Y)) * (p.Magnitude * matGradMag);
							}
							count++;

							// Early termination (greediness)
							double currentAvg = partialSum / count;
							double maxPossible = (partialSum + (totalPoints - count)) / totalPoints;
							if (maxPossible < minScore)
								break;
						}

						double score = count == 0 ? 0 : partialSum / count;

						if (score > minScore)
						{
							matches.Add(new MatchResult
							{
								Position = new Point(x + offset.X, y + offset.Y),
								Score = score,
								Scale = model.Scale,
								Rotation = model.Rotation,
								TemplateSize = new Size(
								(int)(model.BaseTemplateSize.Width),
								(int)(model.BaseTemplateSize .Height))
							});
						}
					}
				}
			});

			return matches;
		}

	}
}

public static class MatExtensions
{
	public static double GetDouble(this Image<Gray, double> img, int row, int col)
	{
		return img[row, col].Intensity;
	}
}
