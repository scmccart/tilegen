# Value noise

## What it does

Generates a grey image of smooth random variation by summing several octaves of value noise: fractal Brownian motion (fBm). It is a source stage, with no input. The threshold reads its output and turns it into a two-terrain map (`threshold.md`, `stage-inputs.md`); the preview's built-in pipeline does exactly that.

The image is built one octave at a time, coarsest first, and each octave is one step of the run. Frame k shows octaves 1 to k summed, so stepping through the frames shows detail being added. The last frame is the stage's output.

It runs as a `Noise` stage through `Generation.Run`, or directly through `ValueNoise.Run(NoiseParameters, Random)`. The engine seeds the `Random` from the stage's seed (`stage-seeds.md`).

## How it works

`ValueNoise.Run` is an iterator. Calling it checks its arguments and does nothing else: nothing is drawn or computed until the first frame is requested. The run then allocates two things, which never escape it:

- an accumulator of one `double` per pixel;
- a working `PixelBuffer`.

Each octave k, from 0 to octaves − 1, is one step.

1. **Draw the lattice.**
   - The octave's spacing is s = CellSize / 2^k pixels, as a `double`. The run starts at the cell size and halves it each octave, which is exact.
   - At the start of the step, the run draws a lattice of `random.NextDouble()` values, each in [0, 1). The lattice is ⌊(W − 1) / s⌋ + 2 columns by ⌊(H − 1) / s⌋ + 2 rows, which is just enough for the last pixel's cell to have a right and a bottom corner.
   - The values are drawn row-major: the top row from left to right, then the next row down.
   - Each octave draws its own lattice, so the octaves are independent. Because the draws happen inside the step, the first frame of an n-octave run equals the output of a 1-octave run with the same seed.
2. **Sample and accumulate.**
   - Pixel (x, y) is sampled at g = (x / s, y / s). Its cell is (i, j) = (⌊x / s⌋, ⌊y / s⌋), and its fractions are t = g − ⌊g⌋ on each axis.
   - Each fraction becomes a smoothstep weight, w = t²(3 − 2t). The curve is flat at both ends, so cell edges don't show as creases.
   - The four corners are blended bilinearly. The top pair and the bottom pair are each blended along x, then the two results along y. Each blend is `a + (b − a) × w`.
   - The octave's amplitude times the blended value is added to the pixel's accumulator. The amplitude is 1 for the first octave and is multiplied by the persistence for each octave after it.
3. **Write the frame.**
   - Each accumulator value is divided by the sum of the amplitudes so far, which gives a value v in [0, 1].
   - The pixel becomes grey `(byte)Math.Round(v × 255, MidpointRounding.AwayFromZero)`, the same in red, green and blue, with alpha 255.
   - The step yields a `Frame` over the working buffer.

A frame is a view, valid until the next step (`functional-core R4`). A caller that keeps a frame calls `ToImage()`, as the run does. The run holds nothing that needs disposing, so it can be abandoned after any frame.

## Parameters

| Parameter | Range | Meaning and effect |
| --- | --- | --- |
| Width, height | 1 to 4096 pixels | The image size. |
| Octaves | 1 to 12 | How many octaves are summed, and so how many frames the run yields. |
| Cell size | 1 to 4096 pixels | The lattice spacing of the first and coarsest octave. Larger values give larger features. |
| Persistence | Above 0, at most 1, finite | Each octave's amplitude relative to the one before. 1 weights every octave equally and gives the roughest image; small values make the fine octaves faint. |

- **The finest-octave rule.** The finest octave's spacing is CellSize / 2^(octaves − 1), and it must be at least one pixel. So the cell size must be at least 2^(octaves − 1): 12 octaves need a cell size of at least 2048.
- **Validation reports it.** A stage that breaks the rule gets a `FinestOctaveBelowOnePixel` finding naming both values and the smallest valid cell size (`NoiseParameters.SmallestValidCellSize`). The rule is checked only when octaves and cell size are both in range.
- **Typed parameters.** `NoiseParameters` can't be built in breach of the rule or of any range. Only validation builds it from a definition's values.

## Sources

- **The book.** David S. Ebert, F. Kenton Musgrave, Darwyn Peachey, Ken Perlin and Steven Worley, *Texturing & Modeling: A Procedural Approach*, 3rd ed., Morgan Kaufmann, 2003.
  - The publisher's contents page, https://shop.elsevier.com/books/texturing-and-modeling/ebert/978-1-55860-848-1, lists:
    - under the chapter "Building Procedural Textures" (cut short to "Building Procedural" on that page), the headings "Making Noises", "Lattice Noises" and "Value Noise", in that order (the list is flat, so how they nest isn't shown);
    - under "A Brief Introduction to Fractals", the heading "Procedural fBm";
    - under "Procedural Fractal Terrains", the heading "Homogeneous fBm Terrain Models".
  - **Chapter numbers.** The pages fetched don't print them. These chapters are the 2nd, 14th and 16th of the 21 in the publisher's list, and Eyrolles' listing gives the same order: https://www.eyrolles.com/Informatique/Livre/texturing-and-modeling-9781558608481/.
  - **Chapter authors are not confirmed for the 3rd edition.** Ebert's contents page for the 2nd edition credits "Building Procedural Textures" to Peachey and "A Brief Introduction to Fractals" to Musgrave: https://userpages.cs.umbc.edu/ebert/book_toc.html.
  - Only the contents were checked. The book's text wasn't available.
- **Scratchapixel, "Value Noise and Procedural Patterns".** Four pages, starting at https://scratchapixel.com/lessons/procedural-generation-virtual-worlds/procedural-patterns-noise-part-1/introduction.html. Checked in its text:
  - "Creating a Simple 2D Noise" puts random values on a lattice. It blends a cell's four corners `c00`, `c10`, `c01` and `c11` along x, then along y, with the fractions remapped by `smoothstep(t) = t * t * (3 - 2 * t)`.
  - "Simple Pattern Examples" sums layers of noise, doubling the frequency and halving the amplitude from layer to layer. It replaces `pow` with repeated multiplication, and calls the general form fBm, with "lacunarity" and "gain".
  - **Where tilegen differs:**
    - The lesson's `lerp` is `lo * (1 - t) + hi * t`, where tilegen uses `a + (b − a) × w`.
    - It uses a fixed 256 × 256 table that wraps, where tilegen's lattice is sized to the image.
    - It normalises the summed image by dividing by its largest value, and mentions clamping as an alternative. Tilegen divides by the sum of the amplitudes instead.
  - The lesson is under CC BY-NC-ND 4.0, which isn't compatible with MIT. Nothing was copied from it.
- **Inigo Quilez, "fBM" (2019),** https://iquilezles.org/articles/fbm/. Checked in its text:
  - The loop is `t += a*noise(f*x); f *= 2.0; a *= G;`, which builds the frequencies and amplitudes by repeated multiplication rather than `pow`.
  - The gain is G = 2^−H, where H is the Hurst exponent, and G = 0.5 is the most common choice. Tilegen's persistence is this gain.
  - The article returns the raw sum `t` and doesn't normalise it.
- **Normalising by the running sum of the amplitudes is tilegen's own step.** Neither web source does it, and the book's text wasn't checked.
- **No code was copied from any source.** The function is written from the description in D5 of the `pipeline-spine` change.

## Decisions and limits

- **Lacunarity is fixed at 2,** and it isn't a parameter. The spacing halves exactly each octave, so the finest-octave rule is a power of two.
- **Normalising by the amplitude sum, not by the image's maximum.**
  - A pixel's value depends only on its own lattice cells, never on other pixels.
  - Every frame is on the same 0-to-1 scale.
  - Frame k equals the output of a k-octave run with the same seed.
  - **The cost:** averaging octaves pulls values towards the middle, so the extremes are rare. The basic golden's pixels span 49 to 227.
- **No clamp.** The value written is always in [0, 1], for three reasons:
  - Lattice values are in [0, 1), and a blend `a + (b − a) × w` of values in [0, 1], with w in [0, 1], rounds to a value in [0, 1]. IEEE rounding is monotone, and the result is bounded by a and by a + round(b − a).
  - The smoothstep weight is in [0, 1].
  - The accumulator and the amplitude sum are summed in the same order, so the quotient is at most 1.

  The form `a(1 − w) + bw` has no such bound and could pass 1 by an ulp; that is why tilegen uses the other. Properties in the tests check the blend and the weight over their whole ranges.
- **Sampled at integer coordinates.** Pixel (x, y) is sampled at (x, y), not at its centre, so pixel (0, 0) sits on a lattice point. With a cell size of 1, every pixel is a raw lattice value: white noise.
- **A different image size usually gives a different image, not a larger crop.**
  - Each octave's lattice is sized to the image and drawn row-major, one octave after another.
  - If an octave's lattice gains a column, its rows start at different draws.
  - If an octave's lattice grows at all, every later octave starts at a different draw.
  - A size change that leaves every lattice the same size changes none of the shared pixels. A taller single-octave image is the shorter one with rows added below.
- **No wrapping.** The lattice covers the image once, so the noise doesn't tile at its edges. Tileable noise is out of scope.
- **Quantization.** The output is 8-bit grey, 256 levels, rounded half away from zero. The `double` accumulator is internal: a later stage sees only the bytes, and a smooth, coarse octave can show bands.
- **Determinism.** The run uses only addition, subtraction, multiplication, division, `Math.Floor` and `Math.Round`, in a fixed order, and draws the lattices in a fixed order. The same parameters and seed give the same pixels on the same .NET version and platform (`functional-core R8`). `System.Random`'s seeded sequence isn't promised across .NET versions, so the golden's provenance records the version.
- **Memory at 4096 × 4096.**
  - The working buffer is 64 MiB, at 4 bytes a pixel.
  - The accumulator is 128 MiB, at 8 bytes a pixel.
  - An octave's lattice exists only during its step. At spacing 1 it is 4097 × 4097 doubles, about 128 MiB.
  - The peak is therefore about 320 MiB, for a stage far larger than a tile needs.

## Where it lives

- `src/Tilegen.Core/Techniques/ValueNoise.cs`: `ValueNoise.Run`, and the internal `Smoothstep` and `Lerp`, which the tests check directly.
- `src/Tilegen.Core/Techniques/NoiseParameters.cs`, `Octaves.cs`, `CellSize.cs`, `Persistence.cs` and `src/Tilegen.Core/Imaging/PixelSize.cs`: the typed parameters, their ranges and `SmallestValidCellSize`.
- `src/Tilegen.Core/Pipelines/Pipeline.cs`: validation of the ranges and the finest-octave rule.
- `src/Tilegen.Core/Pipelines/Generation.cs`: `Generation.Run`, which runs a `Noise` stage with `ValueNoise.Run`.
- `tests/Tilegen.Core.Tests/Techniques/ValueNoiseTests.cs`:
  - frame count and size;
  - the first frame against a 1-octave run;
  - opaque grey;
  - same seed, same pixels, and different seeds differing;
  - a kept copy unaffected by later steps;
  - nothing drawn before the first frame, and each step drawing exactly its own lattice;
  - with cell size 1, each pixel equal to its lattice value.
- `tests/Tilegen.Core.Tests/Techniques/ValueNoiseProperties.cs`: CsCheck properties over generated valid parameters up to 64 × 64 (determinism, one frame per octave, opaque grey), and properties of `Smoothstep` and `Lerp` over [0, 1]. The file says why there is no inputs-unchanged property: value noise has no input. That property is in `ThresholdProperties.cs`, threshold being the first technique that reads another stage's output, and it checks that the threshold leaves its input, such as this stage's output, unchanged after every frame.
- `tests/Tilegen.Core.Tests/Pipelines/GenerationTests.cs`: a validated noise stage gives the same frames through `Generation.Run` as through `ValueNoise.Run`.
- **The golden,** in `tests/Tilegen.Core.Tests/Goldens/value-noise/`:
  - `basic.png`, the image the owner approved by eye (`testing R6`);
  - `basic.golden.json`, its provenance (`testing R5`): technique `value-noise`, 64 × 64, 4 octaves, cell size 16, persistence 0.5, stage seed 20261114, .NET 11.0, approved in `pipeline-spine`.
- **The golden test** is `ValueNoiseGoldenTests.cs`. It reads its parameters and seed from the provenance, runs the stage and compares the last frame.
  - The test project copies `Goldens/` to its output, and the test reads `Goldens/value-noise/` from there.
  - On a failure it writes `basic.actual.png`, and on a mismatch `basic.expected.png` too, to `golden-results/value-noise/` in the test output.
