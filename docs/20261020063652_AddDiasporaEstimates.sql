START TRANSACTION;
CREATE TABLE "DiasporaEstimates" (
    "Id" uuid NOT NULL,
    "CountryId" uuid NOT NULL,
    "CityId" uuid,
    "EstimatedPopulation" integer NOT NULL,
    "SourceName" character varying(200) NOT NULL,
    "SourceUrl" character varying(2048) NOT NULL,
    "SourcePublishedOn" date NOT NULL,
    "IsPublished" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "CreatedByUserId" text,
    "LastModifiedAtUtc" timestamp with time zone,
    "LastModifiedByUserId" text,
    CONSTRAINT "PK_DiasporaEstimates" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_DiasporaEstimates_Cities_CityId" FOREIGN KEY ("CityId") REFERENCES "Cities" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_DiasporaEstimates_Countries_CountryId" FOREIGN KEY ("CountryId") REFERENCES "Countries" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_DiasporaEstimates_CityId" ON "DiasporaEstimates" ("CityId");

CREATE UNIQUE INDEX "IX_DiasporaEstimates_CountryId_CityId" ON "DiasporaEstimates" ("CountryId", "CityId");

CREATE INDEX "IX_DiasporaEstimates_IsPublished" ON "DiasporaEstimates" ("IsPublished");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261020063652_AddDiasporaEstimates', '10.0.11');

COMMIT;

