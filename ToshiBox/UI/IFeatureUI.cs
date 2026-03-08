namespace ToshiBox.UI
{
    public interface IFeatureUI
    {
        string Name { get; }
        bool Enabled { get; set; }
        bool Visible { get; }
        void DrawSettings();
    }
}