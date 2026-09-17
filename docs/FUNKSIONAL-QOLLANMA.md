# Amaliyotchi — Talabalar amaliyotini avtomatlashtirish platformasi

**Funksional qo'llanma — rollar, vazifalar ketma-ketligi va tizim mantig'i**

Versiya: 1.0 · Sana: 14.09.2026
Talaba interfeysi: Telegram Web App (TWA) · Tyutor/Admin interfeysi: veb-dashboard

> Bu hujjat **texnik arxitektura emas**. Bu — tizim *nima qilishi*, *kim nima qila olishi* va
> *har bir jarayon qanday ketma-ketlikda kechishi* haqidagi to'liq funksional tavsif.
> Kod yozish, TZ tayyorlash va buyurtmachi bilan kelishish uchun asos bo'ladi.

---

## 0. Tizim nimani hal qiladi

### 0.1 Hozirgi (qo'lda) holatdagi muammolar

| Muammo | Oqibati |
|---|---|
| Davomat qog'oz jurnalda yuritiladi | Talaba amaliyotga bormasdan ham "bordim" deb yozdiradi |
| Shartnomalar papkada saqlanadi | Yo'qoladi, tekshirish uzoq davom etadi, nusxasi yo'q |
| Tyutor 40–120 talabani qo'lda kuzatadi | Real nazorat imkonsiz, hammasi oxirgi hafta "to'ldiriladi" |
| Kundalik (dnevnik) amaliyot oxirida bir kunda yoziladi | Hisobot haqiqiy ishni aks ettirmaydi |
| Kafedra/dekanat umumiy holatni bilmaydi | Qaror qabul qilish uchun ma'lumot yo'q |
| Baho subyektiv qo'yiladi | Adolatsizlik, e'tirozlar |

### 0.2 Avtomatlashtiriladigan 6 ta jarayon

1. **Rasmiylashtirish** — korxona rekvizitlari + shartnoma raqamli ko'rinishda yuklanadi va tyutor tomonidan moderatsiya qilinadi.
2. **Davomat** — har kuni GPS-geofence bilan isbotlangan check-in / check-out.
3. **Kundalik (portfolio)** — har kuni bajarilgan ish matn + fayl ko'rinishida qayd etiladi.
4. **Nazorat** — tyutor real vaqt rejimida kim qayerda ekanini ko'radi.
5. **Baholash** — davomat foizi va hisobot sifati avtomatik hisoblanadi, tyutor faqat yakunlaydi.
6. **Hisobot** — kafedra/dekanat uchun bir tugma bilan Excel/PDF eksport.

### 0.3 Tizimning asosiy tamoyillari

- **Isbotsiz qayd yo'q.** Har bir davomat yozuvi ortida vaqt belgisi, koordinata va masofa turadi.
- **Hech kim boshqa rol nomidan ish qila olmaydi.** Tyutor ham talaba o'rniga check-in bosa olmaydi — faqat "qo'lda tuzatish" qila oladi va bu audit jurnalida qoladi.
- **Har bir rol faqat o'z ko'lamini ko'radi.** Tyutor — o'z guruhlari, talaba — faqat o'zi.
- **Har bir o'zgarish iz qoldiradi.** Kim, qachon, nimani o'zgartirdi — hammasi yoziladi.

---

## 1. Rollar xaritasi

| Rol | Kim | Asosiy maqsad | Interfeys |
|---|---|---|---|
| **Admin** | Kafedra mudiri / o'quv bo'limi / amaliyot koordinatori | Tizimni sozlash, tyutorlarni boshqarish, umumiy statistika | Veb-dashboard |
| **Tyutor** | Amaliyot rahbari (o'qituvchi) | O'z guruhlari talabalarini boshqarish va baholash | Veb-dashboard |
| **Talaba** | Amaliyot o'tayotgan talaba | Korxona kiritish, har kuni belgilanish, kundalik yuritish | Telegram Web App |
| *Korxona mentori* *(2-faza, ixtiyoriy)* | Korxonadagi mas'ul xodim | Talabaning haqiqatan ishlaganini tasdiqlash, tavsifnoma berish | Cheklangan TWA/veb havola |

**Rollarning ierarxiyasi:** Admin → Tyutor → Talaba. Yuqori rol quyi rolning ma'lumotini **ko'ra oladi**, lekin uning **o'rniga amal bajara olmaydi**.

---

## 2. Umumiy hayotiy tsikl (end-to-end)

```
[0] ADMIN tizimni sozlaydi
     fakultet → yo'nalish → guruh → tyutor akkauntlari → global qoidalar
                     ↓
[1] TYUTOR amaliyot davrini ochadi
     sana oralig'i + kunlik soatlar + kechikish chegarasi + kurs/guruhlar
                     ↓
[2] TYUTOR talabalar ro'yxatini yuklaydi (Excel shablon / qo'lda)
     har bir talabaga Telegram orqali taklif havolasi boradi
                     ↓
[3] TALABA kiradi → profilni to'ldiradi → korxonani kiritadi
     STIR, nomi, manzil, xaritadan nuqta, rahbar, telefon + shartnoma PDF
                     ↓
[4] TYUTOR shartnomani moderatsiya qiladi
     ┌─ Tasdiqlandi → talabaga check-in ochiladi
     ├─ Tuzatish kerak → talaba tahrirlab qayta yuboradi
     └─ Rad etildi → talaba boshqa korxona kiritadi
                     ↓
[5] AMALIYOT DAVRI (har kuni takrorlanadi)
     talaba: kel → check-in → ishla → kundalik yoz → check-out
     tizim: geofence tekshir → status qo'y → eslatma yubor
     tyutor: monitoring → istisnolarni hal qil → hisobotni bahola
                     ↓
[6] YAKUNLASH
     avtomatik davomat % + hisobot ballari → tyutor yakuniy baho qo'yadi
     (ixtiyoriy: korxona tavsifnomasi yuklanadi)
                     ↓
[7] HISOBOT VA ARXIV
     talaba portfoliosi PDF · guruh jadvali Excel · kafedra statistikasi
```

---

## 3. ADMIN — vazifalar ketma-ketligi

### 3.1 Bosqich A — Dastlabki sozlash (bir marta bajariladi)

**A1. Tashkilot ma'lumotlarini kiritish**
- Universitet/institut nomi, logotipi, rasmiy rekvizitlari
- Bu ma'lumotlar keyin barcha PDF hisobotlar sarlavhasida chiqadi

**A2. Tashkiliy strukturani yaratish**
- Fakultetlar → yo'nalishlar (ta'lim yo'nalishi) → kurslar → guruhlar
- Har bir guruhga: guruh nomi (masalan, `412-22`), kurs, yo'nalish, talabalar soni
- Struktura keyinchalik talabalarni import qilishda va filtrlashda ishlatiladi

**A3. Tyutor akkauntlarini yaratish**
- FISH, telefon, email, lavozim
- **Biriktirish:** har bir tyutorga aniq fakultet + yo'nalish + guruh(lar) belgilanadi
- Tizim tyutorga SMS/Telegram orqali bir martalik kirish havolasini yuboradi
- Tyutor birinchi kirishda parolini o'zi o'rnatadi

**A4. Global qoidalarni belgilash (default qiymatlar)**

| Parametr | Misol qiymat | Izoh |
|---|---|---|
| Standart geofence radiusi | 200 m | Tyutor har bir korxona uchun o'zgartira oladi |
| Ruxsat etilgan kechikish | 15 daqiqa | Bundan keyin `Late` statusi |
| Minimal GPS aniqligi | 100 m | Aniqlik yomon bo'lsa check-in qabul qilinmaydi |
| Avtomatik check-out | Kunlik tugash vaqti + 1 soat | Talaba unutgan bo'lsa tizim yopadi |
| Ish kunlari | Dushanba–Shanba | Yakshanba hisobga olinmaydi |
| Kunlik hisobot majburiymi | Ha | Yo'q bo'lsa kun "to'liq emas" hisoblanadi |
| Hisobot uchun minimal belgi | 150 ta belgi | "Ishladim" deb yozishning oldini oladi |

**A5. Bayram va dam olish kunlari kalendarini kiritish**
- Rasmiy bayramlar ro'yxati (8-mart, 21-mart, 1-sentabr va h.k.)
- Bu kunlar davomat hisobidan avtomatik chiqariladi

**A6. Hujjat shablonlarini yuklash**
- Uch tomonlama shartnoma namunasi (talaba yuklab oladi)
- Yo'llanma (napravleniye) shabloni
- Tavsifnoma (xarakteristika) shabloni

### 3.2 Bosqich B — Har o'quv yili takrorlanadigan ishlar

- Yangi o'quv yilini ochish (`2026-2027`)
- Guruhlarni yangi kursga ko'chirish (2-kurs → 3-kurs)
- Bitirgan guruhlarni arxivlash
- Tyutorlar biriktiruvini yangilash
- O'tgan yil ma'lumotlarini arxivga o'tkazish (o'chirilmaydi, faqat "faol emas" bo'ladi)

### 3.3 Bosqich C — Doimiy (kundalik/haftalik) ishlar

**C1. Umumiy dashboardni kuzatish**

Admin ekranida real vaqt ko'rsatkichlari:
- Jami amaliyotchi talabalar soni / shundan faol
- Shartnoma holati: tasdiqlangan / kutilmoqda / rad etilgan / hali yuklamagan
- Bugungi davomat: keldi / kech keldi / kelmadi / hisobot yozmadi
- Fakultetlar kesimida davomat foizi (reyting)
- Tyutorlar faolligi: kim arizalarni necha kunda ko'rib chiqmoqda

**C2. Muammoli nuqtalarni aniqlash**
- 3 kundan ortiq ko'rib chiqilmagan arizalar
- Davomati 70% dan past talabalar ro'yxati
- Bitta korxonaga haddan ko'p talaba biriktirilgan holatlar (soxta amaliyot belgisi)
- Geofence buzilishi ko'p qayd etilgan talabalar

**C3. Hisobotlarni chiqarish**
- Fakultet/yo'nalish/guruh kesimida Excel
- Rektorat uchun umumlashtirilgan PDF
- Istalgan sana oralig'ida

**C4. Audit jurnalini ko'rish**
- Kim qaysi davomatni qo'lda o'zgartirdi
- Kim shartnomani tasdiqladi/rad etdi
- Kim talaba ma'lumotini tahrirladi

### 3.4 Admin NIMA QILA OLMAYDI

- ❌ Talaba nomidan check-in/check-out qila olmaydi
- ❌ Kundalik hisobot yoza olmaydi
- ❌ Yakuniy baho qo'ya olmaydi (bu — tyutorning vakolati)
- ❌ Audit jurnalini o'chira olmaydi
- ✅ Lekin: tyutorning noto'g'ri qarorini bekor qilishi va sababini yozishi mumkin (bu ham logga tushadi)

---

## 4. TYUTOR — vazifalar ketma-ketligi

Tyutor — tizimning **eng faol foydalanuvchisi**. Uning ish oqimi 4 fazaga bo'linadi:
*tayyorgarlik → moderatsiya → kundalik nazorat → yakunlash*.

### FAZA 1 — Tayyorgarlik (amaliyot boshlanishidan 2–4 hafta oldin)

**T1. Tizimga kirish va profilni to'ldirish**
- Admin yuborgan havola orqali kiradi, parol o'rnatadi
- Telefon raqamini tasdiqlaydi (bildirishnomalar uchun)
- Telegram botga ulanadi (ixtiyoriy, lekin tavsiya etiladi)

**T2. Talabalar bazasini shakllantirish**

Uchta usul:

| Usul | Qachon ishlatiladi | Qanday |
|---|---|---|
| **Excel import** | Guruh bo'yicha ommaviy kiritish | Tizim shablon `.xlsx` beradi: **FISH, HEMIS ID, guruh, telefon** (kurs guruhdan olinadi, alohida ustun emas) + "Yo'riqnoma" va mavjud guruhlar ro'yxati varaqlari. To'ldirilgan fayl yuklanadi; tizim xatolarni qator-baqator ko'rsatadi (takroriy ID, bo'sh maydon, noma'lum guruh) va faqat to'g'rilarini qabul qiladi. **Qurilgan** — hozircha admin panelida (`/admin/students` → "Shablon" va "Excel import") |
| ~~HEMIS integratsiyasi~~ | — | **MVP ga kirmaydi**: HEMIS API ruxsati yo'q, bu 2-faza (`QURISH-TARTIBI` M17). Talabalar bazasi Excel import orqali quriladi |
| **Qo'lda kiritish** | 1–2 ta talaba qo'shish | Forma to'ldiriladi |

Har bir talaba yaratilgach, tizim unga **shaxsiy taklif havolasi** beradi (Telegram deep-link yoki SMS).

**T3. Talabalarni tizimga taklif qilish**
- "Hammaga taklif yuborish" tugmasi
- Kim ulandi / kim ulanmagani ro'yxatda ko'rinadi
- Ulanmaganlarga eslatma yuborish mumkin

**T4. Amaliyot davrini yaratish**

Kiritiladigan maydonlar:

| Maydon | Misol | Ahamiyati |
|---|---|---|
| Nomi | "3-kurs ishlab chiqarish amaliyoti" | Hisobotlarda ko'rinadi |
| O'quv yili | 2026-2027 | Arxivlash uchun |
| Kimlar uchun | 3-kurs, 412-22 va 413-22 guruhlar | Kim ko'rishini belgilaydi |
| Boshlanish sanasi | 01.10.2026 | Shu kundan check-in ochiladi |
| Tugash sanasi | 15.11.2026 | Shu kundan keyin yopiladi |
| Kunlik ish vaqti | 09:00 – 17:00 | Check-in/out oynasi |
| Kechikish chegarasi | 15 daqiqa | 09:15 dan keyin `Late` |
| Ish kunlari | Du–Sha | Yakshanba hisoblanmaydi |
| Jami talab qilinadigan kunlar | 36 kun | Davomat % shunga nisbatan hisoblanadi |
| Kundalik hisobot majburiyligi | Ha | |

> **Muhim:** bitta talaba bir vaqtda faqat bitta **faol** davrga tegishli bo'ladi.

**T5. Talabalarni davrga biriktirish**
- Guruh bo'yicha ommaviy biriktirish yoki alohida tanlash
- Biriktirilgan talabaga darhol bildirishnoma boradi: *"Amaliyot davri belgilandi. Korxona ma'lumotlarini 25.09 gacha kiriting"*

### FAZA 2 — Moderatsiya (amaliyot boshlanishidan oldin)

**T6. Kelib tushgan arizalarni ko'rib chiqish**

Tyutor ekranida "Arizalar" bo'limi: `Yangi` / `Tuzatishda` / `Tasdiqlangan` / `Rad etilgan`.

Har bir ariza kartasida ko'rinadi:
- Talaba: FISH, rasm, guruh, HEMIS ID
- Korxona: nomi, STIR (9 xonali), manzil, rahbar FISH, telefon
- Xaritada belgilangan nuqta + taklif qilingan radius
- Yuklangan shartnoma (brauzerda ochib ko'rish mumkin)

**Tekshirish ro'yxati (tyutor nimaga e'tibor beradi):**

- [ ] Shartnomada talabaning FISH si profildagi bilan mos keladimi
- [ ] Korxona nomi shartnomada va formada bir xilmi
- [ ] STIR 9 xonalimi va haqiqiy korxonagami tegishli
- [ ] Shartnoma imzolangan va muhrlanganmi
- [ ] Sanalar amaliyot davriga mos keladimi
- [ ] Xaritadagi nuqta haqiqiy manzilgami (ko'cha ko'rinishidan tekshirish)
- [ ] Korxona faoliyati talabaning yo'nalishiga mos keladimi
- [ ] Radius mantiqiymi (ofis uchun 100–150 m, zavod uchun 300–500 m)

**Uchta qaror:**

| Qaror | Natija | Talaba nimani ko'radi |
|---|---|---|
| ✅ **Tasdiqlash** | Ariza `Approved`, talabaga check-in funksiyasi ochiladi | "Amaliyot joyingiz tasdiqlandi. 01.10 dan belgilanishni boshlashingiz mumkin" |
| ✏️ **Tuzatishga qaytarish** | Ariza `RevisionNeeded`, talaba tahrirlay oladi | Tyutor izohi bilan: "Shartnomada muhr yo'q, qayta yuklang" |
| ❌ **Rad etish** | Ariza `Rejected`, talaba yangisini yaratadi | Sabab bilan: "Korxona yo'nalishingizga mos emas" |

Tasdiqlashda tyutor **radiusni o'zgartira oladi** (masalan, 200 → 120 m).

### FAZA 3 — Kundalik nazorat (amaliyot davomida)

**T7. "Bugun" ekrani — kunning asosiy ish joyi**

Ekran 4 blokka bo'linadi:

```
┌─────────────────────────────────────────────────────┐
│  BUGUN · 12.10.2026 · Chorshanba                    │
│  Keldi: 28   Kech: 3   Kelmadi: 5   Ruxsat: 2      │
├─────────────────────────────────────────────────────┤
│  ⚠ DIQQAT TALAB QILADI                              │
│  • 3 ta talaba radius tashqarisidan urinish qildi   │
│  • 5 ta talaba 10:00 gacha belgilanmadi             │
│  • 2 ta yangi ruxsat so'rovi                        │
├─────────────────────────────────────────────────────┤
│  TALABALAR RO'YXATI (filtrlanadi)                   │
│  Aliyev A.  09:02 ✅ ─ 17:05 ✅  hisobot ✅  120m  │
│  Karimov B. 09:41 🟡 ─ 16:58 ✅  hisobot ⏳  85m   │
│  Sobirov D. ─── ❌ kelmadi                          │
└─────────────────────────────────────────────────────┘
```

**T8. Ko'rish rejimlari**

| Rejim | Nima uchun kerak |
|---|---|
| **Ro'yxat** | Kunlik holatni tez ko'rish, filtrlash (status, guruh, korxona) |
| **Kalendar** | Bitta talabaning butun oyi: qaysi kun kelgan, kech kelgan, kelmagan |
| **Xarita** | Bugun kim qayerdan belgilangani — nuqtalar korxona doiralari bilan |
| **Korxona kesimi** | Bitta korxonadagi barcha talabalar (guruh bo'lib amaliyot o'tayotganlar) |
| **Reyting** | Davomat va hisobot sifati bo'yicha talabalar tartibi |

**T9. Istisnolarni boshqarish**

| Vaziyat | Tyutorning amali |
|---|---|
| Talaba kasal / oilaviy sabab | `Ruxsat so'rovi`ni tasdiqlaydi → kun `Excused` bo'ladi va davomat foiziga salbiy ta'sir qilmaydi |
| Telefoni ishlamadi, lekin haqiqatan kelgan | **Qo'lda check-in** qo'yadi. Tizim yozuvni `Manual (tyutor tomonidan)` deb belgilaydi va sabab talab qiladi |
| GPS xato ko'rsatdi (radius chetida) | Urinish yozuvini ko'rib, tasdiqlaydi yoki radiusni kengaytiradi |
| Talaba soxta lokatsiya ishlatgani shubhasi | Yozuvni `Shubhali` deb belgilaydi, talabadan tushuntirish so'raydi |
| Korxona manzili o'zgardi | Korxona kartasida yangi koordinatani belgilaydi (eski yozuvlarga ta'sir qilmaydi) |
| Talaba korxonani almashtirmoqchi | Eski arizani `Yakunlangan` qiladi, yangisini ochishga ruxsat beradi |

> Har bir qo'lda aralashuv **audit jurnaliga** yoziladi: kim, qachon, nima uchun.

**T10. Kundalik hisobotlarni ko'rib chiqish**
- Yangi hisobotlar lentasi (talaba, sana, matn, fayllar)
- Har biriga: 👍 tasdiqlash · 💬 izoh · ⭐ ball (1–5) · ↩ qayta yozishga qaytarish
- Ommaviy tasdiqlash imkoniyati (sifatli hisobotlar uchun)
- Haftalik yakunda: "Bu hafta 3 ta talaba hisobot yozmadi" ogohlantirishi

### FAZA 4 — Yakunlash (amaliyot tugagach)

**T11. Avtomatik hisob-kitobni ko'rish**

Tizim har bir talaba uchun tayyorlab beradi:
- Jami ish kunlari / qatnashgan kunlar / davomat foizi
- Kech qolgan kunlar soni
- Sababli/sababsiz qoldirilgan kunlar
- Yozilgan hisobotlar soni va o'rtacha bali
- Geofence buzilishi holatlari

**T12. Korxona tavsifnomasini yuklash** *(agar talab qilinsa)*
- Talaba yuklaydi yoki tyutor qo'lda qo'shadi
- Tekshiriladi va portfolioga biriktiriladi

**T13. Yakuniy baho qo'yish**
- Tizim tavsiya etilgan ballni ko'rsatadi (12-bo'limdagi formula bo'yicha)
- Tyutor qabul qiladi yoki o'zgartiradi (o'zgartirsa — sabab yozadi)
- Yakuniy xulosa matni yoziladi

**T14. Hujjatlarni chiqarish**
- Har bir talaba uchun **Portfolio PDF**: profil, korxona, davomat jadvali, barcha kundalik yozuvlar, tyutor xulosasi, baho
- Guruh bo'yicha **davomat jadvali Excel**
- Kafedraga topshiriladigan **umumiy hisobot**

### 4.4 Tyutor NIMA QILA OLMAYDI

- ❌ Boshqa tyutorning guruhini ko'ra olmaydi
- ❌ Talaba nomidan kundalik hisobot yoza olmaydi
- ❌ O'tib ketgan davomat yozuvini **izsiz** o'zgartira olmaydi (har o'zgarish logga tushadi)
- ❌ Tasdiqlangan va yakunlangan amaliyot bahosini admin ruxsatisiz qayta o'zgartira olmaydi
- ❌ Fakultet/guruh strukturasini yarata olmaydi (bu — admin vakolati)

---

## 5. TALABA — vazifalar ketma-ketligi

Talaba butun tizimni **Telegram Web App** orqali ishlatadi: bot bilan suhbat + ichida ochiladigan
mini-ilova. Ilova o'rnatish, parol eslab qolish shart emas.

### FAZA 1 — Ro'yxatdan o'tish (bir marta)

**S1. Botga ulanish**
1. Tyutordan kelgan havolani bosadi (`t.me/amaliyotchi_bot?start=INV_xxxxx`)
2. "Start" bosadi
3. Telefon raqamini ulashadi (Telegram tugmasi orqali) — bu autentifikatsiya
4. Tizim taklif kodini tekshiradi va uni **oldindan kiritilgan talaba yozuvi bilan bog'laydi**
   (FISH, guruh, HEMIS ID allaqachon tyutor tomonidan kiritilgan)

**S2. Profilni to'ldirish**
- Rasm yuklash (portret, hujjatlar uchun)
- Shaxsiy telefon, qo'shimcha aloqa
- FISH/guruh/kurs **tahrirlab bo'lmaydi** — bular tyutor mas'uliyatida (noto'g'ri bo'lsa "Xato bor" tugmasi orqali tyutorga xabar yuboradi)

**S3. Amaliyot shartlari bilan tanishish**
- Ekranda davr sanalari, kunlik soatlar, talab qilinadigan kunlar soni ko'rinadi
- "Tanishdim" tugmasi bosiladi (elektron tasdiq, log'ga yoziladi)

### FAZA 2 — Amaliyot joyini rasmiylashtirish

**S4. Korxona ma'lumotlarini kiritish**

| Qadam | Nima qilinadi |
|---|---|
| 1 | STIR (INN) kiritiladi — 9 xona. Tizim bazadan qidiradi: agar bu korxona ilgari kiritilgan bo'lsa, qolgan maydonlar avtomatik to'ladi |
| 2 | Korxona nomi, faoliyat turi |
| 3 | Manzil (matn) |
| 4 | **Xaritadan nuqta belgilash** — talaba korxona binosining ustiga marker qo'yadi. Ogohlantirish chiqadi: *"Bu nuqta har kuni belgilanish uchun ishlatiladi. Noto'g'ri qo'ysangiz, belgilana olmaysiz"* |
| 5 | Rahbar FISH va telefoni |
| 6 | Mentor (bevosita rahbar) FISH va telefoni |

**S5. Shartnomani yuklash**
- Shablonni yuklab oladi (tizimdan) → chop etadi → korxonada imzolatib, muhrlatib oladi
- Skanerlab yoki suratga olib yuklaydi (PDF yoki JPEG, maksimal hajm cheklangan)
- Sifat tekshiruvi: juda xira rasm avtomatik rad etiladi ("Qayta suratga oling")

**S6. Yuborish va kutish**
- "Tyutorga yuborish" tugmasi
- Holat: 🕓 *Ko'rib chiqilmoqda*
- Bildirishnoma keladi:
  - ✅ *Tasdiqlandi* → check-in bo'limi ochiladi
  - ✏️ *Tuzatish kerak* → izohni o'qiydi, tahrirlaydi, qayta yuboradi
  - ❌ *Rad etildi* → sababni o'qiydi, yangi korxona kiritadi

> Shartnoma tasdiqlanmaguncha talaba **check-in qila olmaydi**. Bu — tizimning qattiq qoidasi.

### FAZA 3 — Har kungi ish tsikli

#### S7. Ertalab — CHECK-IN

```
09:00  Botdan xabar: "Xayrli tong! Belgilanish ochildi. Oyna: 09:00–09:15"
         ↓
  Talaba mini-ilovani ochadi → katta yashil tugma: [ KELDIM ]
         ↓
  Telefon geolokatsiya so'raydi → talaba ruxsat beradi
         ↓
  Tizim 4 ta tekshiruvni bajaradi:
```

| # | Tekshiruv | Muvaffaqiyatsiz bo'lsa chiqadigan xabar |
|---|---|---|
| 1 | Shartnoma tasdiqlanganmi? | "Amaliyot joyingiz hali tasdiqlanmagan" |
| 2 | Bugun ish kunimi va davr ichidami? | "Bugun ish kuni emas" / "Amaliyot davri boshlanmagan" |
| 3 | Vaqt oynasi ichidami? | "Belgilanish 09:00 dan boshlanadi" / "Bugungi belgilanish yopilgan" |
| 4 | GPS aniqligi yetarlimi? (≤100 m) | "Lokatsiya aniq emas. Ochiq joyga chiqing va qayta urinib ko'ring" |
| 5 | **Geofence:** korxonagacha masofa ≤ radius? | "Siz amaliyot joyida emassiz. Korxonagacha ~1,2 km" |

```
  Hammasi to'g'ri bo'lsa:
         ↓
  ✅ "Belgilandingiz! 09:02 · Korxonadan 45 m masofada"
     (kech bo'lsa: 🟡 "Belgilandingiz, lekin 26 daqiqa kech")
```

**Muhim detallar:**
- Kunda **faqat bitta** check-in qabul qilinadi
- Muvaffaqiyatsiz urinishlar ham yoziladi va tyutor ko'radi (masofasi bilan)
- Internet yo'q bo'lsa: lokatsiya qurilmada saqlanadi, tarmoq tiklanganda yuboriladi (urinish vaqti bilan)

#### S8. Kun davomida

- Ekranda: bugungi holat, kelgan vaqt, qolgan vaqt
- Kundalikni istalgan vaqtda yozib borishi mumkin (avtosaqlash bilan qoralama)
- Tyutorning izohlarini ko'radi

#### S9. Kun oxiri — KUNDALIK HISOBOT

Talaba to'ldiradi:

| Maydon | Talab |
|---|---|
| Bugun bajarilgan ishlar | Matn, minimal 150 belgi |
| O'rganilgan yangilik | Matn (ixtiyoriy, lekin baholashda hisobga olinadi) |
| Ilova (foto/hujjat) | 0–5 ta fayl: ish jarayoni surati, tayyorlangan hujjat va h.k. |
| Qiyinchilik/savol | Matn (ixtiyoriy) — tyutorga savol sifatida boradi |

> Tizim bir xil matnni takroran yuborishni aniqlaydi ("kecha bilan bir xil") va ogohlantiradi.

#### S10. CHECK-OUT

```
17:00  Bot: "Ish vaqti tugadi. Ketishni belgilang va kundalikni to'ldiring"
         ↓
  [ KETDIM ] tugmasi → yana geolokatsiya tekshiruvi
         ↓
  ✅ "Kun yakunlandi. Bugun 8 soat 3 daqiqa. Kundalik ✅"
```

- Agar talaba check-out qilmasa: 18:00 da tizim **avtomatik yopadi** va kunni
  `Tugallanmagan (avtomatik yopilgan)` deb belgilaydi — tyutor buni ko'radi
- Check-out ham geofence tekshiruvidan o'tadi (ish joyidan uzoqda "ketdim" bosib bo'lmaydi)

#### S11. Ruxsat so'rash (kelolmasa)

1. "Ruxsat so'rash" tugmasi
2. Sana(lar)ni tanlaydi
3. Sababni yozadi + tasdiqlovchi hujjat yuklaydi (spravka, ariza)
4. Tyutorga boradi → tasdiqlansa, o'sha kun `Excused` bo'ladi va davomat foiziga zarar yetmaydi

### FAZA 4 — Yakunlash

**S12. Portfolioni ko'rish**
- Butun amaliyot davri bo'yicha statistika: qatnashgan kunlar, davomat %, hisobotlar soni
- Barcha kundalik yozuvlar bitta oqimda
- Tyutor izohlari va ballari

**S13. Tavsifnomani yuklash** (agar talab qilinsa)

**S14. Yakuniy bahoni olish**
- Bildirishnoma: "Amaliyotingiz yakunlandi. Baho: 4 (87 ball)"
- Portfolio PDF ni yuklab olishi mumkin

### 5.5 Talaba NIMA QILA OLMAYDI

- ❌ FISH, guruh, kurs, HEMIS ID ni o'zgartira olmaydi
- ❌ Tasdiqlangandan keyin korxona koordinatasini o'zgartira olmaydi (tyutorga murojaat qiladi)
- ❌ O'tib ketgan kunga check-in qo'ya olmaydi
- ❌ Kunlik hisobotni tyutor tasdiqlagandan keyin tahrirlay olmaydi
- ❌ Boshqa talabaning ma'lumotini ko'ra olmaydi
- ❌ Check-in vaqtini yoki koordinatasini qo'lda kirita olmaydi

---

## 6. Bir kunning avtomatik oqimi (tizim o'zi bajaradigan ishlar)

| Vaqt | Tizim nima qiladi |
|---|---|
| 00:05 | Bugungi kunni tayyorlaydi: kim ishlashi kerakligini aniqlaydi (davr + ish kuni + bayram emasligi) |
| 08:45 | Barcha faol talabalarga eslatma: *"15 daqiqadan so'ng belgilanish ochiladi"* |
| 09:00 | Check-in oynasi ochiladi |
| 09:15 | Kechikish chegarasi: shu paytdan keyingi belgilanishlar `Late` |
| 10:00 | Belgilanmaganlarga oxirgi eslatma: *"Hali belgilanmadingiz"* |
| 10:30 | Belgilanmaganlar `Absent` deb qo'yiladi. Tyutorga ro'yxat yuboriladi |
| 16:45 | Eslatma: *"Kundalikni to'ldirishni unutmang"* |
| 17:00 | Check-out oynasi ochiladi |
| 18:00 | Check-out qilmaganlar avtomatik yopiladi (`auto_closed = true`) |
| 18:30 | Hisobot yozmaganlarga eslatma |
| 21:00 | Kun yakunlanadi, statistika qayta hisoblanadi. Tyutorga kunlik xulosa yuboriladi |
| Juma 17:00 | Tyutorga haftalik digest: davomat, muammoli talabalar, kutilayotgan arizalar |
| Har oy 1-sana | Adminga oylik hisobot |

---

## 7. Holatlar (status) mashinalari

### 7.1 Shartnoma/ariza holati

```
  [Qoralama] ──yuborish──> [Ko'rib chiqilmoqda]
                                │
              ┌─────────────────┼──────────────────┐
              ↓                 ↓                  ↓
       [Tasdiqlangan]    [Tuzatish kerak]    [Rad etilgan]
              │                 │                  │
              │            tahrirlab            yangi ariza
              │            qayta yuborish       yaratish
              ↓                 └──────> [Ko'rib chiqilmoqda]
       [Yakunlangan]  (amaliyot tugagach yoki korxona almashtirilganda)
```

### 7.2 Kunlik davomat holati

| Status | Qachon qo'yiladi |
|---|---|
| `Kutilmoqda` | Kun boshlandi, talaba hali belgilanmagan |
| `Keldi` | Vaqt oynasi ichida, radius ichida check-in |
| `Kech keldi` | Kechikish chegarasidan keyin check-in |
| `Kelmadi` | Belgilangan vaqtgacha check-in bo'lmadi |
| `Sababli` | Ruxsat so'rovi tasdiqlangan |
| `Dam olish` | Yakshanba yoki bayram — hisobga kirmaydi |
| `Qo'lda qo'yilgan` | Tyutor qo'lda kiritgan (sabab bilan) |
| `Shubhali` | Geofence buzilishi yoki soxta lokatsiya belgisi |

**Kun to'liq hisoblanishi uchun:** `check-in ✅ + check-out ✅ + kundalik hisobot ✅`
Uchtasidan biri yo'q bo'lsa — kun `Tugallanmagan` deb belgilanadi.

### 7.3 Kundalik hisobot holati

`Qoralama` → `Yuborilgan` → `Tyutor ko'rdi` → `Tasdiqlangan (ball bilan)`
yoki `Qayta yozish kerak` → `Yuborilgan` ...

---

## 8. Biznes qoidalari (tizim mantig'i)

### 8.1 Geofence (eng muhim qoida)

- Masofa **Haversine formulasi** bilan hisoblanadi (yoki PostGIS `ST_DWithin`)
- Shart: `masofa(talaba, korxona) ≤ korxona.radius`
- Radius har bir korxona uchun alohida: ofis 100–150 m, zavod/qurilish 300–500 m
- GPS aniqligi (`accuracy`) 100 m dan yomon bo'lsa — check-in qabul qilinmaydi
- **Har bir urinish yoziladi**, hattoki muvaffaqiyatsizi ham: koordinata, masofa, vaqt

### 8.2 Soxta lokatsiyaga qarshi choralar

| Chora | Qanday ishlaydi |
|---|---|
| Aniqlik filtri | `accuracy > 100 m` → rad etish |
| Tezlik tahlili | Oldingi nuqtadan bugungi nuqtaga fizik jihatdan yetib bo'lmasa → `Shubhali` |
| Bir xil koordinata | Kundan-kunga **aynan** bir xil koordinata (0 m farq) — real GPS bunday bo'lmaydi → belgi qo'yiladi |
| Qurilma barmoq izi | Bitta qurilmadan bir necha talaba belgilansa → ogohlantirish |
| IP va GPS mos kelmasligi | Boshqa viloyat IP si + joyida GPS → belgi |
| Tasodifiy selfi *(ixtiyoriy)* | Haftada 1–2 marta tizim check-in paytida surat so'raydi |

> Bu choralar **avtomatik jazolamaydi** — faqat tyutorga belgi ko'rsatadi. Qarorni odam qabul qiladi.

### 8.3 Vaqt qoidalari

- Check-in faqat `daily_start_time` dan `daily_start_time + N soat` gacha (N sozlanadi)
- Check-out faqat check-in dan keyin va `daily_end_time - 1 soat` dan keyin
- O'tib ketgan kunga hech kim (talaba ham, tyutor ham to'g'ridan-to'g'ri) yozuv qo'sha olmaydi — faqat "qo'lda tuzatish" orqali, sabab bilan
- Barcha vaqtlar server vaqtida saqlanadi (UTC), foydalanuvchiga Toshkent vaqtida ko'rsatiladi

### 8.4 Davomat foizi qanday hisoblanadi

```
Davomat % = (Keldi + Kech keldi + Sababli) / (Jami ish kunlari) × 100

Bunda:
  • Dam olish va bayram kunlari maxrajga kirmaydi
  • "Kech keldi" to'liq kun sifatida hisoblanadi, lekin sifat baliga ta'sir qiladi
  • "Sababli" davomatga zarar qilmaydi, lekin hisobot bali berilmaydi
```

---

## 9. Bildirishnomalar matritsasi

| Hodisa | Kimga | Kanal | Matn (namuna) |
|---|---|---|---|
| Talabaga taklif | Talaba | SMS/Telegram | "Amaliyot tizimiga ulanish: havola" |
| Amaliyot davri belgilandi | Talaba | Bot | "Amaliyot 01.10–15.11. Korxonani 25.09 gacha kiriting" |
| Yangi ariza keldi | Tyutor | Veb + Telegram | "Aliyev A. shartnoma yubordi" |
| Ariza tasdiqlandi | Talaba | Bot | "Amaliyot joyingiz tasdiqlandi ✅" |
| Ariza tuzatishga qaytdi | Talaba | Bot | "Tuzatish kerak: [izoh]" |
| Ertalabki eslatma | Talaba | Bot | "Belgilanish 15 daqiqadan keyin ochiladi" |
| Belgilanmadi | Talaba | Bot | "Hali belgilanmadingiz" |
| Kunlik yakun | Tyutor | Bot/email | "Bugun: 28 keldi, 3 kech, 5 kelmadi" |
| Geofence buzilishi | Tyutor | Veb | "Karimov B. korxonadan 3,4 km uzoqdan urindi" |
| Hisobot eslatmasi | Talaba | Bot | "Kundalikni to'ldiring" |
| Ruxsat so'rovi | Tyutor | Veb + Bot | "Sobirov D. 14.10 uchun ruxsat so'radi" |
| Ruxsat javobi | Talaba | Bot | "Ruxsatingiz tasdiqlandi" |
| Hisobot baholandi | Talaba | Bot | "Kundaligingizga 5 ball qo'yildi" |
| Haftalik digest | Tyutor | Email | Haftalik statistika |
| Ariza 3 kun ko'rilmadi | Admin | Veb | "Tyutor X da 7 ta ko'rilmagan ariza" |
| Amaliyot yakunlandi | Talaba | Bot | "Baho: 4 (87 ball). Portfolioni yuklab oling" |

---

## 10. Ruxsatlar matritsasi (RBAC)

| Amal | Admin | Tyutor | Talaba |
|---|:---:|:---:|:---:|
| Fakultet/yo'nalish/guruh yaratish | ✅ | ❌ | ❌ |
| Tyutor akkaunti yaratish | ✅ | ❌ | ❌ |
| Global qoidalarni sozlash | ✅ | ❌ | ❌ |
| Amaliyot davri yaratish | ✅ | ✅ | ❌ |
| Talaba qo'shish / import | ✅ | ✅ (o'z guruhi) | ❌ |
| Talaba ma'lumotini tahrirlash | ✅ | ✅ (o'z guruhi) | qisman (rasm, telefon) |
| Korxona kiritish | ✅ | ✅ | ✅ (o'zi uchun) |
| Shartnomani tasdiqlash/rad etish | ✅ | ✅ | ❌ |
| Geofence radiusini o'zgartirish | ✅ | ✅ | ❌ |
| Check-in / check-out qilish | ❌ | ❌ | ✅ |
| Qo'lda davomat qo'yish | ✅ (sabab bilan) | ✅ (sabab bilan) | ❌ |
| Ruxsat so'rash | ❌ | ❌ | ✅ |
| Ruxsatni tasdiqlash | ✅ | ✅ | ❌ |
| Kundalik hisobot yozish | ❌ | ❌ | ✅ |
| Hisobotni baholash | ❌ | ✅ | ❌ |
| Yakuniy baho qo'yish | ❌ | ✅ | ❌ |
| Barcha fakultetlar statistikasi | ✅ | ❌ | ❌ |
| O'z guruhi statistikasi | ✅ | ✅ | ❌ |
| O'z portfoliosi | ✅ | ✅ | ✅ |
| Audit jurnali | ✅ | qisman (o'z amallari) | ❌ |
| Ma'lumotni o'chirish | ❌ (faqat arxivlash) | ❌ | ❌ |

---

## 11. Istisno holatlar va ularni hal qilish

| # | Vaziyat | Tizim nima qiladi | Kim hal qiladi |
|---|---|---|---|
| 1 | Telefon zaryadi tugadi / buzildi | Kun `Kelmadi` bo'ladi | Talaba tyutorga murojaat → tyutor qo'lda qo'yadi (sabab bilan) |
| 2 | Internet yo'q | Lokatsiya qurilmada navbatga olinadi, tarmoq tiklanganda yuboriladi (asl vaqt bilan) | Avtomatik |
| 3 | GPS aniqligi yomon (bino ichi) | "Aniq emas" xabari, qayta urinish taklifi | Talaba ochiq joyga chiqadi |
| 4 | Korxona ofisi ko'chdi | Eski koordinata ishlamaydi | Talaba xabar beradi → tyutor korxona kartasini yangilaydi |
| 5 | Talaba korxonani almashtirdi | Eski ariza `Yakunlangan`, yangi ariza ochiladi | Tyutor ruxsat beradi. Eski kunlar saqlanadi va umumiy hisobga qo'shiladi |
| 6 | Ko'chma ish (savdo agenti, quruvchi, kuryer) | Bitta nuqta ishlamaydi | Tyutor korxonaga **bir nechta ruxsat etilgan nuqta** yoki katta radius belgilaydi. Yoki `Ko'chma rejim`: check-in istalgan joydan, lekin surat + izoh majburiy |
| 7 | Masofaviy (remote) amaliyot | Geofence ma'nosiz | Tyutor `Masofaviy rejim` yoqadi: geofence o'chadi, kundalik hisobot va topshiriq majburiy bo'ladi |
| 8 | Bitta telefondan bir necha talaba belgilandi | Qurilma ID takrorlanishi aniqlanadi → `Shubhali` | Tyutor tushuntirish so'raydi |
| 9 | Soxta GPS ilovasi | Tezlik/bir xil koordinata tahlili → belgi | Tyutor qaror qiladi |
| 10 | Talaba kasal bo'lib qoldi | Ruxsat so'rovi + spravka | Tyutor tasdiqlaydi → `Sababli` |
| 11 | Korxona shanba ishlamaydi | Tyutor shu talaba uchun individual ish kunlarini belgilaydi | Tyutor |
| 12 | Amaliyot davri uzaytirildi | Tyutor tugash sanasini o'zgartiradi, hamma talabaga bildirishnoma boradi | Tyutor |
| 13 | Talaba shartnomani muddatida yuklamadi | `Kechikkan` belgisi, tyutorga va adminga signal | Tyutor bilan alohida ishlash |
| 14 | Tyutor kasal / ta'tilda | Admin vaqtincha o'rinbosar tyutor biriktiradi | Admin |
| 15 | Yuklangan shartnoma o'qib bo'lmaydi | Sifat tekshiruvi yoki tyutor `Tuzatish kerak` qiladi | Talaba qayta yuklaydi |
| 16 | Talaba akademik ta'tilga chiqdi | Holat `To'xtatilgan`, davomat hisobi to'xtaydi | Tyutor/Admin |
| 17 | Bitta korxonaga 20 ta talaba | Adminga "shubhali to'planish" signali | Admin tekshiradi |
| 18 | Talaba Telegram akkauntini yo'qotdi | Yangi taklif havolasi generatsiya qilinadi | Tyutor |

---

## 12. Baholash modeli (tavsiya)

Tizim **avtomatik ball** hisoblaydi, tyutor uni tasdiqlaydi yoki o'zgartiradi.

| Komponent | Vazn | Qanday hisoblanadi |
|---|---|---|
| **Davomat** | 40% | Davomat foizi to'g'ridan-to'g'ri ballga aylanadi |
| **Kundalik hisobotlar** | 30% | Yozilgan hisobotlar soni × o'rtacha tyutor bali (1–5) |
| **Tyutor bahosi** | 20% | Tyutorning umumiy xulosasi |
| **Korxona tavsifnomasi** | 10% | Mentor bahosi (agar tizimda bo'lsa) |

```
Misol:
  Davomat 34/36 = 94%        → 37,6 / 40
  Hisobot 32 ta, o'rtacha 4,2 → 25,2 / 30
  Tyutor bahosi 4,5/5         → 18,0 / 20
  Tavsifnoma "a'lo"           → 10,0 / 10
  ───────────────────────────────────────
  JAMI: 90,8 ball  →  Baho: 5
```

**Avtomatik qizil bayroqlar** (tyutor e'tiboriga):
- Davomat < 70% → amaliyot qayta topshirilishi kerak
- Hisobotlar < 50% kun → "hisobot yuritilmagan"
- 3 va undan ko'p `Shubhali` yozuv → tekshirish talab qilinadi

---

## 13. Hisobotlar va eksport

| Hisobot | Kim oladi | Format | Tarkibi |
|---|---|---|---|
| **Talaba portfoliosi** | Talaba, Tyutor | PDF | Profil, korxona, shartnoma, davomat jadvali, barcha kundalik yozuvlar (rasmlari bilan), tyutor xulosasi, baho |
| **Guruh davomat jadvali** | Tyutor | Excel | Satrlar — talabalar, ustunlar — sanalar, kataklar — status |
| **Kunlik hisobot** | Tyutor | Ekran/PDF | Bugun kim keldi, qachon, qayerdan |
| **Amaliyot yakuni hisoboti** | Tyutor → Kafedra | PDF/Excel | Guruh bo'yicha umumiy natijalar, baholar |
| **Fakultet statistikasi** | Admin | Excel/PDF | Yo'nalishlar kesimida davomat, bahoslar taqsimoti |
| **Korxonalar ro'yxati** | Admin | Excel | Qaysi korxonalarda necha talaba amaliyot o'tdi |
| **Audit jurnali** | Admin | Excel | Barcha qo'lda aralashuvlar |

---

## 14. Joriy etish bosqichlari

### Faza 1 — MVP (birinchi navbatda qilinadi)
Maqsad: bitta fakultetda bitta amaliyot davrini to'liq o'tkazish.

- Admin: fakultet/guruh/tyutor yaratish
- Tyutor: talabalarni Excel'dan import, davr yaratish, ariza moderatsiyasi, kundalik monitoring
- Talaba: TWA orqali kirish, korxona + shartnoma, check-in/check-out (geofence), kundalik hisobot
- Bildirishnomalar: Telegram bot orqali asosiylari
- Hisobot: guruh davomat jadvali Excel + talaba portfoliosi PDF

### Faza 2 — Kengaytirish
- Ruxsat so'rovlari tizimi
- Kalendar va xarita ko'rinishlari
- Baholash modeli va avtomatik ball
- Korxona mentori roli va tavsifnoma
- Soxta lokatsiyaga qarshi tahlil
- Admin analitikasi

### Faza 3 — Integratsiya va miqyoslash
- HEMIS API integratsiyasi (talabalar, baholarni qaytarish)
- STIR bo'yicha korxona ma'lumotini avtomatik tortish (soliq bazasi)
- Ko'chma va masofaviy amaliyot rejimlari
- Bir nechta universitetga xizmat ko'rsatish (multi-tenant)
- Mobil ilova (agar TWA yetarli bo'lmasa)

---

## 15. Qaror talab qiladigan ochiq savollar

1. **Shartnoma turi:** uch tomonlama (universitet–korxona–talaba) majburiymi yoki ikki tomonlama ham qabul qilinadimi?
2. **Kim korxonani topadi:** talaba o'zi topadimi yoki kafedra tayyor ro'yxatdan tanlash imkonini beradimi?
3. **Check-in oynasi qanchalik qattiq:** 09:00–09:15 mi yoki 09:00–11:00 (moslashuvchan)?
4. **Kechikish jazolanadimi** yoki faqat qayd etiladimi?
5. **Hafta oxiri:** shanba ish kunimi (korxonaga qarab har xil bo'lishi mumkin)?
6. **Korxona mentori** tizimga kiritiladimi yoki tavsifnoma faqat qog'ozda bo'ladimi?
7. **HEMIS integratsiyasi** mavjudmi va API ruxsati bormi?
8. **Baho HEMIS'ga qaytariladimi** yoki qo'lda kiritiladimi?
9. **Ma'lumotlar saqlash muddati:** bitirgan talabalar arxivi necha yil saqlanadi?
10. **Shaxsiy ma'lumotlar:** geolokatsiya yig'ilishiga talabadan yozma rozilik olinadimi (qonuniy talab)?

---

## Ilova A — Asosiy ekranlar ro'yxati

**Admin (veb):** Dashboard · Fakultetlar · Guruhlar · Tyutorlar · Talabalar (global) · Amaliyot davrlari · Korxonalar · Hisobotlar · Sozlamalar · Audit jurnali

**Tyutor (veb):** Bugun · Talabalarim · Arizalar · Amaliyot davrlari · Kalendar · Xarita · Kundaliklar · Ruxsat so'rovlari · Baholash · Hisobotlar

**Talaba (Telegram Web App):** Bosh ekran (bugungi holat + katta tugma) · Profil · Amaliyot joyim · Kundaligim · Kalendarim · Ruxsat so'rash · Portfolio

---

*Hujjat oxiri. Savol va o'zgartirishlar uchun: ushbu faylni tahrirlang yoki yangi versiya yarating.*
