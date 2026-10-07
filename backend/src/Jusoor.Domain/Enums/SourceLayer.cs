namespace Jusoor.Domain.Enums;

/// <summary>
/// The 7 source layers from spec §05. Phase 0 only needs this enum plus the
/// Source entity to exist so Phase 1's source workers have a schema to write
/// into on day one of that phase — no worker logic is implemented here.
/// </summary>
public enum SourceLayer
{
    Official = 1,              // رسمية — سفارات، وزارة الخارجية، شرطة، مطارات، محاكم
    ProfessionalMedia = 2,     // إعلام مهني موثوق
    DiasporaMedia = 3,         // إعلام الجالية المصرية
    SocialMedia = 4,           // سوشيال ميديا عامة
    UserReports = 5,           // تقارير المستخدمين
    PartnerCorrespondents = 6, // مراسلون شركاء
    CommunityVerification = 7  // تحقق مجتمعي
}
