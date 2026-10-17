using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace AmorRP.Server.Infrastructure.Persistence.Migrations;

// Rename existing data in place; never replace existing communities or asset IDs.
public partial class CommunitiesAndGroups : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE groups RENAME TO communities;
        ALTER TABLE operations RENAME COLUMN "GroupId" TO "CommunityId";
        ALTER TABLE announcements RENAME COLUMN "GroupId" TO "CommunityId";
        ALTER TABLE categories RENAME COLUMN "GroupId" TO "CommunityId";
        ALTER TABLE letters RENAME COLUMN "GroupId" TO "CommunityId";
        ALTER TABLE definition_revisions RENAME COLUMN "GroupId" TO "CommunityId";
        ALTER TABLE invitations RENAME COLUMN "GroupId" TO "CommunityId";
        ALTER TABLE item_definitions RENAME COLUMN "GroupId" TO "CommunityId";
        ALTER TABLE media_assets RENAME COLUMN "GroupId" TO "CommunityId";
        ALTER TABLE memberships RENAME COLUMN "GroupId" TO "CommunityId";
        ALTER TABLE ownership_transfers RENAME COLUMN "GroupId" TO "CommunityId";
        ALTER TABLE holdings RENAME COLUMN "GroupId" TO "CommunityId";
        ALTER TABLE quota_usage RENAME COLUMN "GroupId" TO "CommunityId";
        ALTER TABLE trades RENAME COLUMN "GroupId" TO "CommunityId";
        ALTER TABLE trade_lines RENAME COLUMN "GroupId" TO "CommunityId";
        ALTER TABLE trade_slots RENAME COLUMN "GroupId" TO "CommunityId";
        ALTER TABLE letters RENAME CONSTRAINT "FK_letters_categories_GroupId_CategoryId" TO "FK_letters_categories_CommunityId_CategoryId";
        ALTER TABLE definition_revisions RENAME CONSTRAINT "FK_definition_revisions_categories_GroupId_CategoryId" TO "FK_definition_revisions_categories_CommunityId_CategoryId";
        ALTER TABLE communities RENAME CONSTRAINT "PK_groups" TO "PK_communities";
        ALTER TABLE communities RENAME CONSTRAINT "ck_group_policy" TO "ck_community_policy";
        ALTER TABLE communities RENAME CONSTRAINT "FK_groups_characters_CreatedByCharacterId" TO "FK_communities_characters_CreatedByCharacterId";
        ALTER TABLE communities RENAME CONSTRAINT "FK_groups_characters_OwnerCharacterId" TO "FK_communities_characters_OwnerCharacterId";
        ALTER TABLE invitations RENAME CONSTRAINT "FK_invitations_groups_GroupId" TO "FK_invitations_communities_CommunityId";
        ALTER TABLE item_definitions RENAME CONSTRAINT "FK_item_definitions_groups_GroupId" TO "FK_item_definitions_communities_CommunityId";
        ALTER TABLE media_assets RENAME CONSTRAINT "FK_media_assets_groups_GroupId" TO "FK_media_assets_communities_CommunityId";
        ALTER TABLE memberships RENAME CONSTRAINT "FK_memberships_groups_GroupId" TO "FK_memberships_communities_CommunityId";
        ALTER TABLE ownership_transfers RENAME CONSTRAINT "FK_ownership_transfers_groups_GroupId" TO "FK_ownership_transfers_communities_CommunityId";
        ALTER TABLE holdings RENAME CONSTRAINT "FK_holdings_categories_GroupId_CategoryId" TO "FK_holdings_categories_CommunityId_CategoryId";
        ALTER TABLE holdings RENAME CONSTRAINT "FK_holdings_definition_revisions_GroupId_DefinitionId_Definiti~" TO "FK_holdings_definition_revisions_CommunityId_DefinitionId_Defi~";
        ALTER TABLE holdings RENAME CONSTRAINT "FK_holdings_letters_GroupId_LetterId" TO "FK_holdings_letters_CommunityId_LetterId";
        ALTER TABLE holdings RENAME CONSTRAINT "FK_holdings_memberships_GroupId_CharacterId" TO "FK_holdings_memberships_CommunityId_CharacterId";
        ALTER TABLE quota_usage RENAME CONSTRAINT "FK_quota_usage_memberships_GroupId_CharacterId" TO "FK_quota_usage_memberships_CommunityId_CharacterId";
        ALTER TABLE trades RENAME CONSTRAINT "FK_trades_memberships_GroupId_InitiatorId" TO "FK_trades_memberships_CommunityId_InitiatorId";
        ALTER TABLE trades RENAME CONSTRAINT "FK_trades_memberships_GroupId_RecipientId" TO "FK_trades_memberships_CommunityId_RecipientId";
        ALTER TABLE trade_lines RENAME CONSTRAINT "FK_trade_lines_holdings_GroupId_HoldingId" TO "FK_trade_lines_holdings_CommunityId_HoldingId";
        ALTER TABLE trade_lines RENAME CONSTRAINT "FK_trade_lines_trades_GroupId_TradeId" TO "FK_trade_lines_trades_CommunityId_TradeId";
        ALTER TABLE trade_slots RENAME CONSTRAINT "FK_trade_slots_memberships_GroupId_CharacterId" TO "FK_trade_slots_memberships_CommunityId_CharacterId";
        ALTER TABLE trade_slots RENAME CONSTRAINT "FK_trade_slots_trades_GroupId_TradeId" TO "FK_trade_slots_trades_CommunityId_TradeId";
        ALTER INDEX "IX_announcements_GroupId_Sequence" RENAME TO "IX_announcements_CommunityId_Sequence";
        ALTER INDEX "IX_definition_revisions_GroupId_CategoryId" RENAME TO "IX_definition_revisions_CommunityId_CategoryId";
        ALTER INDEX "IX_groups_CreatedByCharacterId" RENAME TO "IX_communities_CreatedByCharacterId";
        ALTER INDEX "IX_groups_CurrencyId" RENAME TO "IX_communities_CurrencyId";
        ALTER INDEX "IX_groups_Id_CurrencyIconAssetId" RENAME TO "IX_communities_Id_CurrencyIconAssetId";
        ALTER INDEX "IX_groups_Id_LogoAssetId" RENAME TO "IX_communities_Id_LogoAssetId";
        ALTER INDEX "IX_groups_OwnerCharacterId" RENAME TO "IX_communities_OwnerCharacterId";
        ALTER INDEX "IX_holdings_GroupId_CategoryId" RENAME TO "IX_holdings_CommunityId_CategoryId";
        ALTER INDEX "IX_holdings_GroupId_CharacterId_DefinitionId_DefinitionRevision" RENAME TO "IX_holdings_CommunityId_CharacterId_DefinitionId_DefinitionRev~";
        ALTER INDEX "IX_holdings_GroupId_CharacterId_TypeId" RENAME TO "IX_holdings_CommunityId_CharacterId_TypeId";
        ALTER INDEX "IX_holdings_GroupId_DefinitionId_DefinitionRevision" RENAME TO "IX_holdings_CommunityId_DefinitionId_DefinitionRevision";
        ALTER INDEX "IX_holdings_GroupId_LetterId" RENAME TO "IX_holdings_CommunityId_LetterId";
        ALTER INDEX "IX_invitations_GroupId" RENAME TO "IX_invitations_CommunityId";
        ALTER INDEX "IX_letters_GroupId_CategoryId" RENAME TO "IX_letters_CommunityId_CategoryId";
        ALTER INDEX "IX_operations_GroupId_OccurredAt_Id" RENAME TO "IX_operations_CommunityId_OccurredAt_Id";
        ALTER INDEX "IX_ownership_transfers_GroupId_Status" RENAME TO "IX_ownership_transfers_CommunityId_Status";
        ALTER INDEX "IX_trade_lines_GroupId_HoldingId" RENAME TO "IX_trade_lines_CommunityId_HoldingId";
        ALTER INDEX "IX_trade_slots_GroupId_TradeId" RENAME TO "IX_trade_slots_CommunityId_TradeId";
        ALTER INDEX "IX_trades_GroupId_InitiatorId" RENAME TO "IX_trades_CommunityId_InitiatorId";
        ALTER INDEX "IX_trades_GroupId_RecipientId" RENAME TO "IX_trades_CommunityId_RecipientId";
        ALTER TABLE media_assets DROP CONSTRAINT ck_media_bounds;
        UPDATE media_assets SET "Purpose" = 'community_logo' WHERE "Purpose" = 'group_logo';
        ALTER TABLE media_assets ADD CONSTRAINT ck_media_bounds CHECK (("Purpose" = 'currency' AND "Width" BETWEEN 1 AND 128 AND "Height" BETWEEN 1 AND 128 AND octet_length("Png") <= 131072) OR ("Purpose" IN ('community_logo', 'group_logo') AND "Width" BETWEEN 1 AND 512 AND "Height" BETWEEN 1 AND 512 AND octet_length("Png") <= 1048576));
        UPDATE memberships SET "Capabilities" = array_replace("Capabilities", 'group.moderate', 'community.moderate');
        CREATE TABLE groups (
            "Id" uuid NOT NULL,
            "CommunityId" uuid NOT NULL,
            "Name" character varying(80) NOT NULL,
            "Description" text NOT NULL,
            "LogoAssetId" uuid,
            "LogoVersion" integer NOT NULL,
            "Version" integer NOT NULL,
            "Deleted" boolean NOT NULL,
            CONSTRAINT "PK_groups" PRIMARY KEY ("CommunityId", "Id"),
            CONSTRAINT "FK_groups_communities_CommunityId" FOREIGN KEY ("CommunityId") REFERENCES communities ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_groups_media_assets_CommunityId_LogoAssetId" FOREIGN KEY ("CommunityId", "LogoAssetId") REFERENCES media_assets ("CommunityId", "Id") ON DELETE RESTRICT
        );
        CREATE INDEX "IX_groups_CommunityId_LogoAssetId" ON groups ("CommunityId", "LogoAssetId");
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $$ BEGIN IF EXISTS (SELECT 1 FROM groups) THEN RAISE EXCEPTION 'Cannot downgrade while community groups exist; keep this server and use the stable plugin instead.'; END IF; END $$;
        DROP TABLE groups;
        ALTER TABLE media_assets DROP CONSTRAINT ck_media_bounds;
        UPDATE media_assets SET "Purpose" = 'group_logo' WHERE "Purpose" = 'community_logo';
        ALTER TABLE media_assets ADD CONSTRAINT ck_media_bounds CHECK (("Purpose" = 'currency' AND "Width" BETWEEN 1 AND 128 AND "Height" BETWEEN 1 AND 128 AND octet_length("Png") <= 131072) OR ("Purpose" = 'group_logo' AND "Width" BETWEEN 1 AND 512 AND "Height" BETWEEN 1 AND 512 AND octet_length("Png") <= 1048576));
        UPDATE memberships SET "Capabilities" = array_replace("Capabilities", 'community.moderate', 'group.moderate');
        ALTER INDEX "IX_trades_CommunityId_RecipientId" RENAME TO "IX_trades_GroupId_RecipientId";
        ALTER INDEX "IX_trades_CommunityId_InitiatorId" RENAME TO "IX_trades_GroupId_InitiatorId";
        ALTER INDEX "IX_trade_slots_CommunityId_TradeId" RENAME TO "IX_trade_slots_GroupId_TradeId";
        ALTER INDEX "IX_trade_lines_CommunityId_HoldingId" RENAME TO "IX_trade_lines_GroupId_HoldingId";
        ALTER INDEX "IX_ownership_transfers_CommunityId_Status" RENAME TO "IX_ownership_transfers_GroupId_Status";
        ALTER INDEX "IX_operations_CommunityId_OccurredAt_Id" RENAME TO "IX_operations_GroupId_OccurredAt_Id";
        ALTER INDEX "IX_letters_CommunityId_CategoryId" RENAME TO "IX_letters_GroupId_CategoryId";
        ALTER INDEX "IX_invitations_CommunityId" RENAME TO "IX_invitations_GroupId";
        ALTER INDEX "IX_holdings_CommunityId_LetterId" RENAME TO "IX_holdings_GroupId_LetterId";
        ALTER INDEX "IX_holdings_CommunityId_DefinitionId_DefinitionRevision" RENAME TO "IX_holdings_GroupId_DefinitionId_DefinitionRevision";
        ALTER INDEX "IX_holdings_CommunityId_CharacterId_TypeId" RENAME TO "IX_holdings_GroupId_CharacterId_TypeId";
        ALTER INDEX "IX_holdings_CommunityId_CharacterId_DefinitionId_DefinitionRev~" RENAME TO "IX_holdings_GroupId_CharacterId_DefinitionId_DefinitionRevision";
        ALTER INDEX "IX_holdings_CommunityId_CategoryId" RENAME TO "IX_holdings_GroupId_CategoryId";
        ALTER INDEX "IX_communities_OwnerCharacterId" RENAME TO "IX_groups_OwnerCharacterId";
        ALTER INDEX "IX_communities_Id_LogoAssetId" RENAME TO "IX_groups_Id_LogoAssetId";
        ALTER INDEX "IX_communities_Id_CurrencyIconAssetId" RENAME TO "IX_groups_Id_CurrencyIconAssetId";
        ALTER INDEX "IX_communities_CurrencyId" RENAME TO "IX_groups_CurrencyId";
        ALTER INDEX "IX_communities_CreatedByCharacterId" RENAME TO "IX_groups_CreatedByCharacterId";
        ALTER INDEX "IX_definition_revisions_CommunityId_CategoryId" RENAME TO "IX_definition_revisions_GroupId_CategoryId";
        ALTER INDEX "IX_announcements_CommunityId_Sequence" RENAME TO "IX_announcements_GroupId_Sequence";
        ALTER TABLE trade_slots RENAME CONSTRAINT "FK_trade_slots_trades_CommunityId_TradeId" TO "FK_trade_slots_trades_GroupId_TradeId";
        ALTER TABLE trade_slots RENAME CONSTRAINT "FK_trade_slots_memberships_CommunityId_CharacterId" TO "FK_trade_slots_memberships_GroupId_CharacterId";
        ALTER TABLE trade_lines RENAME CONSTRAINT "FK_trade_lines_trades_CommunityId_TradeId" TO "FK_trade_lines_trades_GroupId_TradeId";
        ALTER TABLE trade_lines RENAME CONSTRAINT "FK_trade_lines_holdings_CommunityId_HoldingId" TO "FK_trade_lines_holdings_GroupId_HoldingId";
        ALTER TABLE trades RENAME CONSTRAINT "FK_trades_memberships_CommunityId_RecipientId" TO "FK_trades_memberships_GroupId_RecipientId";
        ALTER TABLE trades RENAME CONSTRAINT "FK_trades_memberships_CommunityId_InitiatorId" TO "FK_trades_memberships_GroupId_InitiatorId";
        ALTER TABLE quota_usage RENAME CONSTRAINT "FK_quota_usage_memberships_CommunityId_CharacterId" TO "FK_quota_usage_memberships_GroupId_CharacterId";
        ALTER TABLE holdings RENAME CONSTRAINT "FK_holdings_memberships_CommunityId_CharacterId" TO "FK_holdings_memberships_GroupId_CharacterId";
        ALTER TABLE holdings RENAME CONSTRAINT "FK_holdings_letters_CommunityId_LetterId" TO "FK_holdings_letters_GroupId_LetterId";
        ALTER TABLE holdings RENAME CONSTRAINT "FK_holdings_definition_revisions_CommunityId_DefinitionId_Defi~" TO "FK_holdings_definition_revisions_GroupId_DefinitionId_Definiti~";
        ALTER TABLE holdings RENAME CONSTRAINT "FK_holdings_categories_CommunityId_CategoryId" TO "FK_holdings_categories_GroupId_CategoryId";
        ALTER TABLE ownership_transfers RENAME CONSTRAINT "FK_ownership_transfers_communities_CommunityId" TO "FK_ownership_transfers_groups_GroupId";
        ALTER TABLE memberships RENAME CONSTRAINT "FK_memberships_communities_CommunityId" TO "FK_memberships_groups_GroupId";
        ALTER TABLE media_assets RENAME CONSTRAINT "FK_media_assets_communities_CommunityId" TO "FK_media_assets_groups_GroupId";
        ALTER TABLE item_definitions RENAME CONSTRAINT "FK_item_definitions_communities_CommunityId" TO "FK_item_definitions_groups_GroupId";
        ALTER TABLE invitations RENAME CONSTRAINT "FK_invitations_communities_CommunityId" TO "FK_invitations_groups_GroupId";
        ALTER TABLE communities RENAME CONSTRAINT "FK_communities_characters_OwnerCharacterId" TO "FK_groups_characters_OwnerCharacterId";
        ALTER TABLE communities RENAME CONSTRAINT "FK_communities_characters_CreatedByCharacterId" TO "FK_groups_characters_CreatedByCharacterId";
        ALTER TABLE communities RENAME CONSTRAINT "ck_community_policy" TO "ck_group_policy";
        ALTER TABLE communities RENAME CONSTRAINT "PK_communities" TO "PK_groups";
        ALTER TABLE definition_revisions RENAME CONSTRAINT "FK_definition_revisions_categories_CommunityId_CategoryId" TO "FK_definition_revisions_categories_GroupId_CategoryId";
        ALTER TABLE letters RENAME CONSTRAINT "FK_letters_categories_CommunityId_CategoryId" TO "FK_letters_categories_GroupId_CategoryId";
        ALTER TABLE trade_slots RENAME COLUMN "CommunityId" TO "GroupId";
        ALTER TABLE trade_lines RENAME COLUMN "CommunityId" TO "GroupId";
        ALTER TABLE trades RENAME COLUMN "CommunityId" TO "GroupId";
        ALTER TABLE quota_usage RENAME COLUMN "CommunityId" TO "GroupId";
        ALTER TABLE holdings RENAME COLUMN "CommunityId" TO "GroupId";
        ALTER TABLE ownership_transfers RENAME COLUMN "CommunityId" TO "GroupId";
        ALTER TABLE memberships RENAME COLUMN "CommunityId" TO "GroupId";
        ALTER TABLE media_assets RENAME COLUMN "CommunityId" TO "GroupId";
        ALTER TABLE item_definitions RENAME COLUMN "CommunityId" TO "GroupId";
        ALTER TABLE invitations RENAME COLUMN "CommunityId" TO "GroupId";
        ALTER TABLE definition_revisions RENAME COLUMN "CommunityId" TO "GroupId";
        ALTER TABLE letters RENAME COLUMN "CommunityId" TO "GroupId";
        ALTER TABLE categories RENAME COLUMN "CommunityId" TO "GroupId";
        ALTER TABLE announcements RENAME COLUMN "CommunityId" TO "GroupId";
        ALTER TABLE operations RENAME COLUMN "CommunityId" TO "GroupId";
        ALTER TABLE communities RENAME TO groups;
        """);
}
