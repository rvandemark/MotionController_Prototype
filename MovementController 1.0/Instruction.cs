﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MovementController_1._0
{
    abstract class Instruction
    {
        public AZELCoordinate destinationCoordinates;
        public DateTime destinationTime;
        public DateTime startTime;

        public Instruction(AZELCoordinate destCoords, DateTime destTime)
        {
            destinationCoordinates = destCoords;
            destinationTime = destTime;
        }

        public Instruction(decimal az, decimal el, DateTime dest) : this(new AZELCoordinate(az, el), dest) {}

        // This will be used later when this Instruction is starting its execution, to recalibrate
        // its actual start time instead of assuming its exactly on time
        public void setStartTime(DateTime st) { startTime = st; }

        // Abstract implementation
        public abstract AZELCoordinate CoordinateAtTime(decimal dt, AZELCoordinate curr);
    }

    class SlewInstruction : Instruction
    {
        public SlewInstruction(decimal az, decimal el, DateTime dest) : base(az, el, dest) { }

        public override AZELCoordinate CoordinateAtTime(decimal elapsedTime, AZELCoordinate startCoordinates)
        {
            int timeInterval = destinationTime.Subtract(startTime).Seconds;

            if (timeInterval > 0 && elapsedTime > 0)
            {
                decimal mAZ = (destinationCoordinates.azimuth - startCoordinates.azimuth) / timeInterval;
                decimal mEL = (destinationCoordinates.elevation - startCoordinates.elevation) / timeInterval;
                return new AZELCoordinate(
                    (mAZ * elapsedTime) + startCoordinates.azimuth,
                    (mEL * elapsedTime) + startCoordinates.elevation
                );
            }
            else
            {
                return startCoordinates;
            }
        }
    }

    class TrackInstruction : Instruction
    {
        public CelestialBody body;

        public TrackInstruction(decimal az, decimal el, DateTime dest, CelestialBody bo) : base(az, el, dest)
        {
            body = bo;
        }
        public TrackInstruction(decimal az, decimal el, DateTime dest)
            : this(az, el, dest, TempContainer.OBJ_STAR) { }

        public override AZELCoordinate CoordinateAtTime(decimal elapsedTime, AZELCoordinate startCoordinates)
        {
            int destinationElapsedTime = destinationTime.Subtract(startTime).Seconds;

            if (destinationElapsedTime > 0 && elapsedTime > 0)
            {
                // This is wrong, we have to actually find arc length

                AZELCoordinate result = new AZELCoordinate(0, 0);

                decimal b = destinationCoordinates.azimuth - startCoordinates.azimuth;
                decimal timeDone = elapsedTime / destinationElapsedTime;
                decimal azDone = timeDone * b;

                result.elevation = body.height * (timeDone * timeDone) / (b * b);
                result.azimuth = azDone + startCoordinates.azimuth;

                return result;
            }
            else
            {
                return startCoordinates;
            }
        }
    }

    // NOT STABLE, PRODUCES WEIRD RESULTS
    class DriftScanInstruction : Instruction
    {
        private const decimal SCAN_DROP_DEGREES = 0.5m;

        public DriftScanInstruction(decimal az, decimal el, DateTime dest) : base(az, el, dest) { }

        public override AZELCoordinate CoordinateAtTime(decimal elapsedTime, AZELCoordinate startCoordinates)
        {
            int destinationElapsedTime = destinationTime.Subtract(startTime).Seconds;

            if (destinationElapsedTime > 0 && elapsedTime > 0)
            {
                // Assume change in azimuth and change in elevation are both positive
                decimal dAZ = destinationCoordinates.azimuth - startCoordinates.azimuth;
                decimal dEL = destinationCoordinates.elevation - startCoordinates.elevation;

                // Every DriftScan must be at least one change in azimuth across
                decimal pathLength = dAZ;

                // Track how much change in elevation is left
                decimal remainingEL = dEL;

                while (remainingEL > 2 * SCAN_DROP_DEGREES)
                {
                    pathLength += ((2 * dAZ) + (2 * SCAN_DROP_DEGREES));
                    remainingEL -= (2 * SCAN_DROP_DEGREES);
                }

                // Ignore this case for now (assume the difference in position is an
                // exact interval of 2*SCAN_DROP_DEGREES)
                // pathLength += remainingEL;

                decimal portionDone = pathLength * (elapsedTime / destinationElapsedTime);

                AZELCoordinate cumulative = new AZELCoordinate(0, 0);
                decimal accPath = 0;
                decimal sequence = 0;

                while (true)
                {
                    switch (sequence)
                    {
                        case 0:
                            if (portionDone - accPath > dAZ)
                            {
                                accPath += dAZ;
                            }
                            else
                            {
                                cumulative.azimuth = portionDone - accPath;
                                return cumulative;
                            }
                            break;

                        case 1:
                            if (portionDone - accPath > SCAN_DROP_DEGREES)
                            {
                                accPath += SCAN_DROP_DEGREES;
                                cumulative.elevation += SCAN_DROP_DEGREES;
                            }
                            else
                            {
                                cumulative.elevation += portionDone - accPath;
                                cumulative.azimuth = dAZ;
                                return cumulative;
                            }
                            break;

                        case 2:
                            if (portionDone - accPath > dAZ)
                            {
                                accPath += dAZ;
                            }
                            else
                            {
                                cumulative.azimuth = dAZ - portionDone + accPath;
                                return cumulative;
                            }
                            break;

                        case 3:
                            if (portionDone - accPath > SCAN_DROP_DEGREES)
                            {
                                accPath += SCAN_DROP_DEGREES;
                                cumulative.elevation += SCAN_DROP_DEGREES;
                            }
                            else
                            {
                                cumulative.elevation += portionDone - accPath;
                                cumulative.azimuth = 0;
                                return cumulative;
                            }
                            break;
                    }

                    sequence = (sequence + 1) % 4;

                    Console.WriteLine(
                        accPath.ToString() + " : " + portionDone.ToString() + " : " +
                        cumulative.azimuth.ToString() + " : " + cumulative.elevation.ToString()
                    );
                }
            }
            else
            {
                return startCoordinates;
            }
        }
    }
}