using System.Drawing;
using System.Numerics;
using Engine.Cameras;
using Silk.NET.Maths;
using Rectangle = System.Drawing.Rectangle;

namespace Renderer.Display;

public class CameraManager
{
    // TODO: laat alle cameras één array aan ints delen voor garbage collection optimization, via slices
    /// <summary>
    /// The camera manager manages all cameras and the belonging textures, optimizing memory allocations.
    /// </summary>
    public CameraManager(Size displaySize, CameraLayout initialCameraLayout)
    {
        DisplaySize = displaySize;
        CameraLayout = initialCameraLayout;
        CameraSlots = new List<CameraSlot>();

        NumberOfVisibleCameras = 0;
        _currentCameraSlotIndex = 0;
    }

    public    Size             DisplaySize   { get; private set; }
    public    CameraLayout     CameraLayout { get; protected set; }
    protected List<CameraSlot> CameraSlots  { get; set; }

    public int NumberOfCameras => CameraSlots.Count;
    private Camera CurrentCamera => CameraSlots[_currentCameraSlotIndex].Camera;

    public int NumberOfVisibleCameras;
    private int _currentCameraSlotIndex;


    /// <summary>
    /// The only method which sets the managed properties of the camera slots.
    /// </summary>
    /// <remarks>
    /// Only those slots which are visible will be updated. Properties of undisplayed slots might be outdated.
    /// </remarks>
    private void UpdateDisplayedCameraSlots()
    {
        switch (CameraLayout)
        {
            case CameraLayout.Single:
                CameraSlots[_currentCameraSlotIndex].Update(new Point(0, 0), DisplaySize, DisplaySize);
                break;
            case CameraLayout.Matrix:
                // Determine dimensions for each slot
                int numberOfColumns = (int)Math.Ceiling(MathF.Sqrt(NumberOfVisibleCameras));
                int numberOfRows = (int)MathF.Ceiling((float)NumberOfVisibleCameras / numberOfColumns);
                int slotWidth = DisplaySize.Width / numberOfColumns;
                int slotHeight = DisplaySize.Height / numberOfRows;

                // Set the location and dimensions of each slot
                for (int i = 0; i < NumberOfVisibleCameras; i++)
                {
                    int camX = i % numberOfColumns;
                    int camY = i / numberOfColumns;
                    var slotPos = new Point(camX * slotWidth, camY * slotHeight);
                    var slotSize = new Size(slotWidth, slotHeight);

                    CameraSlots[(_currentCameraSlotIndex + i) % NumberOfCameras].Update(slotPos, slotSize, DisplaySize);
                }
                break;
            default:
                throw new NotImplementedException();
        }
    }

    public IEnumerable<CameraSlot> GetDisplayedCameraSlots()
    {
        switch (CameraLayout)
        {
            case CameraLayout.Single:
                yield return CameraSlots[_currentCameraSlotIndex];
                break;
            case CameraLayout.Matrix:
                for (int i = 0; i < NumberOfVisibleCameras; i++)
                    yield return CameraSlots[(_currentCameraSlotIndex + i) % NumberOfCameras];
                break;
            default:
                throw new NotImplementedException();
        }
    }


    /// <summary>
    /// Adds a camera to this state machine, but does not set its <see cref="Camera.ImageSize"/>.
    /// </summary>
    /// <remarks>The <see cref="Camera.ImageSize"/> will be set the next time it is displayed.</remarks>
    public void AddCamera(Camera camera)
    {
        CameraSlots.Add(new CameraSlot(camera, new OpenGLTexture(0, 0), new Rectangle()));
        NumberOfVisibleCameras++;
        UpdateDisplayedCameraSlots();
    }
    
    public void AddBasicCamera(Vector3 position, int depth, float fov=65)
    {
        AddCamera(new SampledCamera(
            position,
            Vector3.UnitY, Vector3.UnitZ,
            new Size(),
            fov,
            depth));
    }

    public void RemoveCurrentCamera()
    {
        CameraSlots.RemoveAt(_currentCameraSlotIndex);
        UpdateDisplayedCameraSlots();
    }

    public void SetLayout(CameraLayout layout)
    {
        CameraLayout = layout;
        UpdateDisplayedCameraSlots();
    }

    public void FocusOnCamera(int cameraIndex)
    {
        if (cameraIndex < 0 || cameraIndex >= NumberOfVisibleCameras)
            throw new ArgumentOutOfRangeException(nameof(cameraIndex));

        _currentCameraSlotIndex = cameraIndex;
        UpdateDisplayedCameraSlots();
    }

    public void ResizeDisplay(Vector2D<int> displaySize)
    {
        DisplaySize = new Size(displaySize.X, displaySize.Y);
        UpdateDisplayedCameraSlots();
    }

    #region Controls

    public void CycleThroughLayout()
    {
        CameraLayout = CameraLayout == CameraLayout.Single
            ? CameraLayout.Matrix
            : CameraLayout.Single;
        UpdateDisplayedCameraSlots();
    }

    public void CycleFocusThroughCameras(int skip = 1)
    {
        FocusOnCamera((_currentCameraSlotIndex + skip + NumberOfCameras) % NumberOfCameras);
        UpdateDisplayedCameraSlots();
    }

    public void SetNumberOfVisibleCameras(int amount)
    {
        if (amount > NumberOfCameras)
            amount = NumberOfCameras;
        if (amount < 1)
            amount = 1;

        NumberOfVisibleCameras = amount;
        UpdateDisplayedCameraSlots();
    }

    public void IncreaseNumberOfVisibleCameras(int amount = 1) => SetNumberOfVisibleCameras(NumberOfVisibleCameras + amount);

    public void DecreaseNumberOfVisibleCameras(int amount = 1) => IncreaseNumberOfVisibleCameras(-amount);

    public void MoveFocussedCameraForward(float amount)      => CurrentCamera.MoveForward(amount);
    public void MoveFocussedCameraHorizontally(float amount) => CurrentCamera.MoveHorizontally(amount);
    public void MoveFocussedCameraVertically(float amount)   => CurrentCamera.MoveVertically(amount);

    public void RotateFocussedCameraHorizontally(float degree) => CurrentCamera.RotateHorizontally(degree);
    public void RotateFocussedCameraVertically(float degree) => CurrentCamera.RotateVertically(degree);

    public void ZoomFocussedCamera(float amount) => CurrentCamera.Zoom(amount);

    #endregion
}

public enum CameraLayout
{
    Single,
    Matrix
}
