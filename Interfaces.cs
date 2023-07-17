namespace Binject {

    /// <summary>
    /// <para> Makes the implementing class be known to the dependency injection system as an injectable type. Which you'll be
    /// then able to use either from Unity Inspector or from code. </para>
    /// </summary>
    public interface IBDependency { }


    /// <summary>
    /// Marks the implementing class as one that has a custom editor to be drawn with. It's only useful alongside
    /// <see cref="IBDependency"/>.
    /// </summary>
    public interface IBHasCustomDrawer { }

}