namespace Kitchen.EditorTools
{
    /// <summary>
    /// Identifies the state of the generated demo, so a scene or an acceptance report can be traced
    /// back to the generator that produced it.
    /// </summary>
    public static class KitchenVersion
    {
        /// <summary>Increments when the generated content or HUD layout changes materially.</summary>
        public const string Version = "1.3.0";

        /// <summary>Short summary of what this version generates.</summary>
        public const string Summary =
            "Perspective 3D top down camera framed to the whole map, Chinese HUD with a dynamic CJK " +
            "font, and a visible kitchen preview authored into the scene.";
    }
}
