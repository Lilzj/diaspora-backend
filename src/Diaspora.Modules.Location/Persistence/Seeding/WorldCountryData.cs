namespace Diaspora.Modules.Location.Persistence.Seeding;

/// <summary>
/// The shipped country table: ISO alpha-2 code, English name, and E.164 calling code.
/// <para>
/// Transcribed from the frontend's <c>src/mocks/countries.ts</c> so the two lists cannot
/// disagree. That matters more than it sounds: the app bundles this list today and renders it
/// on the origin, residence and phone-input screens, so a name here that differed from a name
/// there would show a member two spellings of their own country depending on which screen
/// they were on. The parity is asserted by test.
/// </para>
/// <para>
/// Names are display text, not contract — a country renamed by ISO is edited here without a
/// version bump. Codes are contract. The list is data rather than a lookup against
/// <c>RegionInfo</c> because ICU's names differ from the frontend's in about a dozen places
/// ("Bosnia and Herzegovina" against "Bosnia &amp; Herzegovina"), and matching the client
/// exactly is worth more here than tracking ICU.
/// </para>
/// </summary>
public static class WorldCountryData
{
    /// <summary>
    /// The countries Diaspora operates in at launch. Everything else is listed so a member can
    /// name where they are from, but is not a market — the distinction the
    /// <c>IsSupported</c> flag carries.
    /// </summary>
    public static readonly IReadOnlySet<string> SupportedAtLaunch =
        new HashSet<string>(StringComparer.Ordinal) { "GB", "MU" };

    /// <summary>Surfaced above the alphabetical list on selection screens.</summary>
    public static readonly IReadOnlySet<string> FeaturedAtLaunch =
        new HashSet<string>(StringComparer.Ordinal) { "GB", "MU", "NG", "GH", "KE", "ZA" };

    /// <summary>Code, name, calling code — 195 entries, in the frontend's own order.</summary>
    public static readonly IReadOnlyList<WorldCountryRecord> All =
    [
        new("AF", "Afghanistan", "+93"), new("AL", "Albania", "+355"),
        new("DZ", "Algeria", "+213"), new("AD", "Andorra", "+376"),
        new("AO", "Angola", "+244"), new("AR", "Argentina", "+54"),
        new("AM", "Armenia", "+374"), new("AU", "Australia", "+61"),
        new("AT", "Austria", "+43"), new("AZ", "Azerbaijan", "+994"),
        new("BS", "Bahamas", "+1"), new("BH", "Bahrain", "+973"),
        new("BD", "Bangladesh", "+880"), new("BB", "Barbados", "+1"),
        new("BY", "Belarus", "+375"), new("BE", "Belgium", "+32"),
        new("BZ", "Belize", "+501"), new("BJ", "Benin", "+229"),
        new("BT", "Bhutan", "+975"), new("BO", "Bolivia", "+591"),
        new("BA", "Bosnia & Herzegovina", "+387"), new("BW", "Botswana", "+267"),
        new("BR", "Brazil", "+55"), new("BN", "Brunei", "+673"),
        new("BG", "Bulgaria", "+359"), new("BF", "Burkina Faso", "+226"),
        new("BI", "Burundi", "+257"), new("KH", "Cambodia", "+855"),
        new("CM", "Cameroon", "+237"), new("CA", "Canada", "+1"),
        new("CV", "Cape Verde", "+238"), new("CF", "Central African Republic", "+236"),
        new("TD", "Chad", "+235"), new("CL", "Chile", "+56"),
        new("CN", "China", "+86"), new("CO", "Colombia", "+57"),
        new("KM", "Comoros", "+269"), new("CG", "Congo - Brazzaville", "+242"),
        new("CD", "Congo - Kinshasa", "+243"), new("CR", "Costa Rica", "+506"),
        new("CI", "Côte d'Ivoire", "+225"), new("HR", "Croatia", "+385"),
        new("CU", "Cuba", "+53"), new("CY", "Cyprus", "+357"),
        new("CZ", "Czechia", "+420"), new("DK", "Denmark", "+45"),
        new("DJ", "Djibouti", "+253"), new("DM", "Dominica", "+1"),
        new("DO", "Dominican Republic", "+1"), new("EC", "Ecuador", "+593"),
        new("EG", "Egypt", "+20"), new("SV", "El Salvador", "+503"),
        new("GQ", "Equatorial Guinea", "+240"), new("ER", "Eritrea", "+291"),
        new("EE", "Estonia", "+372"), new("SZ", "Eswatini", "+268"),
        new("ET", "Ethiopia", "+251"), new("FJ", "Fiji", "+679"),
        new("FI", "Finland", "+358"), new("FR", "France", "+33"),
        new("GA", "Gabon", "+241"), new("GM", "Gambia", "+220"),
        new("GE", "Georgia", "+995"), new("DE", "Germany", "+49"),
        new("GH", "Ghana", "+233"), new("GR", "Greece", "+30"),
        new("GD", "Grenada", "+1"), new("GT", "Guatemala", "+502"),
        new("GN", "Guinea", "+224"), new("GW", "Guinea-Bissau", "+245"),
        new("GY", "Guyana", "+592"), new("HT", "Haiti", "+509"),
        new("HN", "Honduras", "+504"), new("HU", "Hungary", "+36"),
        new("IS", "Iceland", "+354"), new("IN", "India", "+91"),
        new("ID", "Indonesia", "+62"), new("IR", "Iran", "+98"),
        new("IQ", "Iraq", "+964"), new("IE", "Ireland", "+353"),
        new("IL", "Israel", "+972"), new("IT", "Italy", "+39"),
        new("JM", "Jamaica", "+1"), new("JP", "Japan", "+81"),
        new("JO", "Jordan", "+962"), new("KZ", "Kazakhstan", "+7"),
        new("KE", "Kenya", "+254"), new("KI", "Kiribati", "+686"),
        new("KP", "North Korea", "+850"), new("KR", "South Korea", "+82"),
        new("KW", "Kuwait", "+965"), new("KG", "Kyrgyzstan", "+996"),
        new("LA", "Laos", "+856"), new("LV", "Latvia", "+371"),
        new("LB", "Lebanon", "+961"), new("LS", "Lesotho", "+266"),
        new("LR", "Liberia", "+231"), new("LY", "Libya", "+218"),
        new("LI", "Liechtenstein", "+423"), new("LT", "Lithuania", "+370"),
        new("LU", "Luxembourg", "+352"), new("MG", "Madagascar", "+261"),
        new("MW", "Malawi", "+265"), new("MY", "Malaysia", "+60"),
        new("MV", "Maldives", "+960"), new("ML", "Mali", "+223"),
        new("MT", "Malta", "+356"), new("MH", "Marshall Islands", "+692"),
        new("MR", "Mauritania", "+222"), new("MU", "Mauritius", "+230"),
        new("MX", "Mexico", "+52"), new("FM", "Micronesia", "+691"),
        new("MD", "Moldova", "+373"), new("MC", "Monaco", "+377"),
        new("MN", "Mongolia", "+976"), new("ME", "Montenegro", "+382"),
        new("MA", "Morocco", "+212"), new("MZ", "Mozambique", "+258"),
        new("MM", "Myanmar", "+95"), new("NA", "Namibia", "+264"),
        new("NR", "Nauru", "+674"), new("NP", "Nepal", "+977"),
        new("NL", "Netherlands", "+31"), new("NZ", "New Zealand", "+64"),
        new("NI", "Nicaragua", "+505"), new("NE", "Niger", "+227"),
        new("NG", "Nigeria", "+234"), new("NO", "Norway", "+47"),
        new("OM", "Oman", "+968"), new("PK", "Pakistan", "+92"),
        new("PW", "Palau", "+680"), new("PA", "Panama", "+507"),
        new("PG", "Papua New Guinea", "+675"), new("PY", "Paraguay", "+595"),
        new("PE", "Peru", "+51"), new("PH", "Philippines", "+63"),
        new("PL", "Poland", "+48"), new("PT", "Portugal", "+351"),
        new("QA", "Qatar", "+974"), new("RO", "Romania", "+40"),
        new("RU", "Russia", "+7"), new("RW", "Rwanda", "+250"),
        new("KN", "Saint Kitts & Nevis", "+1"), new("LC", "Saint Lucia", "+1"),
        new("VC", "Saint Vincent", "+1"), new("WS", "Samoa", "+685"),
        new("SM", "San Marino", "+378"), new("ST", "São Tomé & Príncipe", "+239"),
        new("SA", "Saudi Arabia", "+966"), new("SN", "Senegal", "+221"),
        new("RS", "Serbia", "+381"), new("SC", "Seychelles", "+248"),
        new("SL", "Sierra Leone", "+232"), new("SG", "Singapore", "+65"),
        new("SK", "Slovakia", "+421"), new("SI", "Slovenia", "+386"),
        new("SB", "Solomon Islands", "+677"), new("SO", "Somalia", "+252"),
        new("ZA", "South Africa", "+27"), new("SS", "South Sudan", "+211"),
        new("ES", "Spain", "+34"), new("LK", "Sri Lanka", "+94"),
        new("SD", "Sudan", "+249"), new("SR", "Suriname", "+597"),
        new("SE", "Sweden", "+46"), new("CH", "Switzerland", "+41"),
        new("SY", "Syria", "+963"), new("TJ", "Tajikistan", "+992"),
        new("TZ", "Tanzania", "+255"), new("TH", "Thailand", "+66"),
        new("TL", "Timor-Leste", "+670"), new("TG", "Togo", "+228"),
        new("TO", "Tonga", "+676"), new("TT", "Trinidad & Tobago", "+1"),
        new("TN", "Tunisia", "+216"), new("TR", "Turkey", "+90"),
        new("TM", "Turkmenistan", "+993"), new("TV", "Tuvalu", "+688"),
        new("UG", "Uganda", "+256"), new("UA", "Ukraine", "+380"),
        new("AE", "United Arab Emirates", "+971"), new("GB", "United Kingdom", "+44"),
        new("US", "United States", "+1"), new("UY", "Uruguay", "+598"),
        new("UZ", "Uzbekistan", "+998"), new("VU", "Vanuatu", "+678"),
        new("VE", "Venezuela", "+58"), new("VN", "Vietnam", "+84"),
        new("YE", "Yemen", "+967"), new("ZM", "Zambia", "+260"),
        new("ZW", "Zimbabwe", "+263")
    ];
}
