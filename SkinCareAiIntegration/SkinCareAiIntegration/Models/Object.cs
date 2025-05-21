using System;
using NcnnDotNet.OpenCV;

namespace SkinCareAiIntegration.Models
{
    public sealed class FaceObject
    {

        #region Constructors

        public FaceObject()
        {
            this.Rect = new Rect<float>();
        }

        #endregion

        #region Properties

        public Rect<float> Rect
        {
            get;
            set;
        }

        public int Label
        {
            get;
            set;
        }

        public float Prob
        {
            get;
            set;
        }

        #endregion

    }
}