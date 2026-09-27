namespace topg.Web.Quiz.Execution;

/// <param name="Turns">How often the centre of the image is turned around itself; the corners stay in place.</param>
/// <param name="BlurWidth">
/// Width in pixels the swirled image is shrunk to and then enlarged from, which blurs it the same way on every screen
/// size. Null keeps it sharp.
/// </param>
/// <param name="Grey">How much colour is taken away, from 0 (full colour) to 1 (greyscale).</param>
public record ObscureLevel(double Turns, int? BlurWidth, double Grey);
