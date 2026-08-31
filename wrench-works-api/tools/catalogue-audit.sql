\echo '=== 1. Trims that are just an engine size (the bug class already fixed) ==='
SELECT mk."Name" make, mo."Name" model, v."Trim", v."EngineDisplacementL"
FROM "VehicleVariants" v
JOIN "VehicleModels" mo ON mo."Id"=v."ModelId"
JOIN "VehicleMakes" mk ON mk."Id"=mo."MakeId"
WHERE v."Trim" ~ '^[0-9]+\.[0-9]+$';

\echo ''
\echo '=== 2. Exact duplicates (same model/years/trim/body/engine/transmission/fuel) ==='
SELECT mo."Name" model, v."Trim", v."EngineDisplacementL", v."Transmission", count(*)
FROM "VehicleVariants" v JOIN "VehicleModels" mo ON mo."Id"=v."ModelId"
GROUP BY mo."Name", v."Trim", v."BodyStyle", v."EngineDisplacementL", v."Transmission", v."FuelType", v."YearFrom", v."YearTo"
HAVING count(*) > 1;

\echo ''
\echo '=== 3. Overlapping year ranges for the same model+trim+engine+transmission ==='
SELECT mo."Name" model, a."Trim", a."EngineDisplacementL" disp,
       a."YearFrom"||'-'||a."YearTo" AS range_a, b."YearFrom"||'-'||b."YearTo" AS range_b
FROM "VehicleVariants" a
JOIN "VehicleVariants" b ON a."ModelId"=b."ModelId" AND a."Id" < b."Id"
JOIN "VehicleModels" mo ON mo."Id"=a."ModelId"
WHERE a."Trim" IS NOT DISTINCT FROM b."Trim"
  AND a."EngineDisplacementL" IS NOT DISTINCT FROM b."EngineDisplacementL"
  AND a."Transmission" = b."Transmission"
  AND a."YearFrom" <= b."YearTo" AND b."YearFrom" <= a."YearTo";

\echo ''
\echo '=== 4. Trims offered in only ONE transmission (may be right, may be a missing row) ==='
SELECT mk."Name" make, mo."Name" model, v."Trim", v."EngineDisplacementL" disp,
       string_agg(DISTINCT v."Transmission", ', ') AS transmissions
FROM "VehicleVariants" v
JOIN "VehicleModels" mo ON mo."Id"=v."ModelId"
JOIN "VehicleMakes" mk ON mk."Id"=mo."MakeId"
GROUP BY mk."Name", mo."Name", v."Trim", v."EngineDisplacementL"
HAVING count(DISTINCT v."Transmission") = 1
ORDER BY mk."Name", mo."Name";

\echo ''
\echo '=== 5. Year coverage per model — gaps between generations show as jumps ==='
SELECT mk."Name" make, mo."Name" model,
       min(v."YearFrom") first_year, max(v."YearTo") last_year,
       count(*) variants
FROM "VehicleVariants" v
JOIN "VehicleModels" mo ON mo."Id"=v."ModelId"
JOIN "VehicleMakes" mk ON mk."Id"=mo."MakeId"
GROUP BY mk."Name", mo."Name" ORDER BY mk."Name", mo."Name";

\echo ''
\echo '=== 6. Models with NO variants (dead ends in the cascade) ==='
SELECT mk."Name" make, mo."Name" model
FROM "VehicleModels" mo
JOIN "VehicleMakes" mk ON mk."Id"=mo."MakeId"
LEFT JOIN "VehicleVariants" v ON v."ModelId"=mo."Id"
WHERE v."Id" IS NULL;

\echo ''
\echo '=== 7. Body styles per model (a model with many bodies but one trim is suspicious) ==='
SELECT mk."Name" make, mo."Name" model, string_agg(DISTINCT v."BodyStyle", ', ') bodies,
       string_agg(DISTINCT v."Trim", ', ') trims
FROM "VehicleVariants" v
JOIN "VehicleModels" mo ON mo."Id"=v."ModelId"
JOIN "VehicleMakes" mk ON mk."Id"=mo."MakeId"
GROUP BY mk."Name", mo."Name" ORDER BY mk."Name";
