using System;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Difficulty.Relax;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Relax
{
    [TestFixture]
    public class RelaxPreprocessingTest
    {
        [TestCase(4f, 36.48f, 1.4254386f)]
        [TestCase(7f, 23.04f, 2.4826388f)]
        public void TestGeometry(float circleSize, float expectedRadius, float expectedScalingFactor)
        {
            var (radius, scalingFactor) = RelaxDifficultyCalculation.Geometry(circleSize);

            Assert.That(radius, Is.EqualTo(expectedRadius).Within(1e-4));
            Assert.That(scalingFactor, Is.EqualTo(expectedScalingFactor).Within(1e-5));
        }

        [Test]
        public void TestCircle()
        {
            var obj = new RelaxObject(new HitCircle { StartTime = 1000, Position = new Vector2(10, 20) }, 36.48f, 1);

            Assert.That(obj.Time, Is.EqualTo(1000f));
            Assert.That(obj.EndPosition, Is.EqualTo(new Vector2(10, 20)));
            Assert.That(obj.TravelDistance, Is.EqualTo(0f));
            Assert.That(obj.IsSpinner, Is.False);
        }

        [Test]
        public void TestSpinner()
        {
            var obj = new RelaxObject(new Spinner { StartTime = 1000, Duration = 2000, Position = new Vector2(256, 192) }, 36.48f, 1);

            Assert.That(obj.IsSpinner, Is.True);
            Assert.That(obj.TravelDistance, Is.Null);
        }

        [Test]
        public void TestSliderLazyEnd()
        {
            // 200px linear slider at 0.1px/ms: tick at 100px, legacy last tick at 1964ms (196.4px).
            var obj = new RelaxObject(createSlider(0), 36.48f, 2);

            Assert.That(obj.EndPosition.X, Is.EqualTo(196.4f - 36.48f * 3).Within(1e-3));
            Assert.That(obj.EndPosition.Y, Is.EqualTo(0).Within(1e-5));
            Assert.That(obj.TravelDistance, Is.EqualTo((196.4f - 36.48f * 3) * 2).Within(1e-3));
        }

        [Test]
        public void TestRepeatSliderLazyEnd()
        {
            // The repeat at 200px pulls the cursor; the second tick (100px) and mirrored last tick (3.6px) stay inside the follow circle.
            var obj = new RelaxObject(createSlider(1), 36.48f, 2);

            Assert.That(obj.EndPosition.X, Is.EqualTo(200 - 36.48f * 3).Within(1e-3));
            Assert.That(obj.TravelDistance, Is.EqualTo((200 - 36.48f * 3) * 2).Within(1e-3));
        }

        [Test]
        public void TestMovement()
        {
            var a = new RelaxObject(new HitCircle { StartTime = 0, Position = new Vector2(0, 0) }, 36.48f, 1);
            var b = new RelaxObject(new HitCircle { StartTime = 100, Position = new Vector2(100, 0) }, 36.48f, 1);
            var c = new RelaxObject(new HitCircle { StartTime = 200, Position = new Vector2(100, 100) }, 36.48f, 1);

            var first = RelaxMovement.Compute(b, a, null, 1.5f, true);
            var second = RelaxMovement.Compute(c, b, a, 1.5f, true);

            Assert.That(first.JumpDistance, Is.EqualTo(150f).Within(1e-4));
            Assert.That(first.Angle, Is.Null);
            Assert.That(first.NormalisedVectorAngle, Is.Null);

            Assert.That(second.JumpDistance, Is.EqualTo(150f).Within(1e-4));
            Assert.That(second.Angle, Is.EqualTo(MathF.PI / 2).Within(1e-5));
            Assert.That(second.NormalisedVectorAngle, Is.EqualTo(MathF.PI / 2).Within(1e-5));
        }

        [Test]
        public void TestMovementWithoutSliderFollowing()
        {
            var slider = new RelaxObject(createSlider(0), 36.48f, 2);
            var circle = new RelaxObject(new HitCircle { StartTime = 3000, Position = new Vector2(300, 0) }, 36.48f, 2);

            var with = RelaxMovement.Compute(circle, slider, null, 2, true);
            var without = RelaxMovement.Compute(circle, slider, null, 2, false);

            Assert.That(with.TravelDistance, Is.EqualTo(slider.TravelDistance));
            Assert.That(with.JumpDistance, Is.EqualTo((300 - slider.EndPosition.X) * 2).Within(1e-3));
            Assert.That(without.TravelDistance, Is.EqualTo(0f));
            Assert.That(without.JumpDistance, Is.EqualTo(600f).Within(1e-3));
        }

        [Test]
        public void TestSpinnerMovement()
        {
            var a = new RelaxObject(new HitCircle { StartTime = 0, Position = new Vector2(0, 0) }, 36.48f, 1);
            var b = new RelaxObject(new HitCircle { StartTime = 100, Position = new Vector2(100, 0) }, 36.48f, 1);
            var spinner = new RelaxObject(new Spinner { StartTime = 200, Duration = 1000, Position = new Vector2(256, 192) }, 36.48f, 1);

            var movement = RelaxMovement.Compute(spinner, b, a, 1, true);

            Assert.That(movement.JumpDistance, Is.EqualTo(0f));
            Assert.That(movement.NormalisedVectorAngle, Is.Null);
            Assert.That(movement.Angle, Is.Not.Null);
        }

        private static Slider createSlider(int repeats)
        {
            var slider = new Slider
            {
                StartTime = 0,
                Position = Vector2.Zero,
                RepeatCount = repeats,
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(200, 0) }),
            };

            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty { SliderMultiplier = 1, SliderTickRate = 1 });

            return slider;
        }
    }
}
