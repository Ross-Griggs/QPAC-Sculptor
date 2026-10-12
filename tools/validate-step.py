"""Validate a STEP file with OpenCascade: reports file units, solid count, B-rep validity,
volume and bounding box (in the file's own length unit).

    pip install cadquery-ocp
    python tools/validate-step.py model.step [--expect-volume V] [--expect-solids N]

Exit code is non-zero if any solid is invalid or an expectation is not met.
"""
import argparse
import sys

from OCP.BRepCheck import BRepCheck_Analyzer
from OCP.Bnd import Bnd_Box
from OCP.BRepBndLib import BRepBndLib
from OCP.GProp import GProp_GProps
from OCP.BRepGProp import BRepGProp
from OCP.IFSelect import IFSelect_RetDone
from OCP.Interface import Interface_Static
from OCP.STEPControl import STEPControl_Reader
from OCP.OCP.collections import Sequence_TCollection_AsciiString
from OCP.TopAbs import TopAbs_SOLID
from OCP.TopExp import TopExp_Explorer


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("path")
    ap.add_argument("--expect-volume", type=float)
    ap.add_argument("--expect-solids", type=int)
    ap.add_argument("--tolerance", type=float, default=0.01, help="relative volume tolerance")
    args = ap.parse_args()

    reader = STEPControl_Reader()
    if reader.ReadFile(args.path) != IFSelect_RetDone:
        print("FAIL: could not parse file")
        return 2

    lengths, angles, solids = (Sequence_TCollection_AsciiString() for _ in range(3))
    reader.FileUnits(lengths, angles, solids)
    unit = lengths.Value(1).ToCString() if lengths.Length() else "?"

    # Keep geometry in the file's unit instead of OpenCascade's default conversion to mm.
    Interface_Static.SetCVal_s("xstep.cascade.unit", unit if unit in ("INCH", "MM", "M", "CM", "FT") else "MM")
    reader.TransferRoots()
    shape = reader.OneShape()

    ok = True
    count = 0
    total = 0.0
    exp = TopExp_Explorer(shape, TopAbs_SOLID)
    while exp.More():
        solid = exp.Current()
        count += 1
        if not BRepCheck_Analyzer(solid).IsValid():
            print(f"INVALID solid #{count}")
            ok = False
        props = GProp_GProps()
        BRepGProp.VolumeProperties_s(solid, props)
        if props.Mass() <= 0:
            print(f"NON-POSITIVE volume solid #{count}: {props.Mass()}")
            ok = False
        total += props.Mass()
        exp.Next()

    box = Bnd_Box()
    BRepBndLib.Add_s(shape, box)
    lo, hi = box.CornerMin(), box.CornerMax()
    x0, y0, z0, x1, y1, z1 = lo.X(), lo.Y(), lo.Z(), hi.X(), hi.Y(), hi.Z()
    print(f"unit={unit} solids={count} volume={total:.6f} "
          f"bbox=({x0:.4f},{y0:.4f},{z0:.4f})..({x1:.4f},{y1:.4f},{z1:.4f}) valid={ok}")

    if args.expect_solids is not None and count != args.expect_solids:
        print(f"FAIL: expected {args.expect_solids} solids")
        ok = False
    if args.expect_volume is not None and abs(total - args.expect_volume) > args.tolerance * abs(args.expect_volume):
        print(f"FAIL: expected volume {args.expect_volume}")
        ok = False
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
