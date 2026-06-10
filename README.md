# 💻 VibeLearn Java IDE Mini

**VibeLearn Java IDE Mini** là một ứng dụng Web IDE rút gọn dành cho lập trình viên Java, được phát triển trên nền tảng **ASP.NET Core 8 (Razor Pages)** và **Supabase (PostgreSQL & Auth)**. 

Hệ thống cho phép người dùng viết, lưu trữ, quản lý các dự án hướng đối tượng (OOP) đa tệp tin/nhiều package và thực thi mã nguồn Java trực tiếp trên môi trường máy chủ tương thích **Java 17 (JDK 17)** thông qua cơ chế tự biên dịch nội bộ (Self-hosted). Đây cũng là một dự án mẫu thực tế giúp các nhà phát triển chuyển đổi từ hệ sinh thái Java Spring Boot sang C# .NET Core một cách trực quan nhất.

---

## ✨ Các Tính Năng Nổi Bật

- 📂 **Cấu trúc cây thư mục NetBeans (Package Tree View)**: Hiển thị danh sách file phân cấp thư mục lồng nhau chuyên nghiệp, hỗ trợ đóng/mở thư mục, và các thanh co giãn (Resizable Panels) bằng chuột linh hoạt.
- ⚡ **Biên dịch & Thực thi tự thân (Self-hosted JDK 17)**: Hệ thống chạy biên dịch trực tiếp thông qua JDK 17 cài trên môi trường local và container Docker/Railway, đảm bảo độ ổn định cao và không phụ thuộc API ngoài.
- 🎨 **Monaco Editor cao cấp**: Bộ soạn thảo mã nguồn giống VS Code với theme tối (Dark Mode) và tô màu cú pháp (syntax highlighting) cho Java. Tự động gợi ý code (autocomplete) đã được tắt hoàn toàn giúp lập trình viên rèn luyện tư duy tự viết code.
- 🔐 **Xác thực an toàn (Authentication)**: Đăng nhập bằng Email truyền thống hoặc **Google OAuth** thông qua Supabase Auth. Sử dụng HttpOnly Cookies bảo vệ JWT chống tấn công XSS.
- 🛡️ **Hạn mức quota (Rate Limiting)**: Giới hạn 200 lượt chạy code mỗi ngày trên mỗi tài khoản để bảo vệ hệ thống tránh bị spam và khai thác tài nguyên quá mức.
- 🔗 **Chia sẻ & Fork dự án**: Tạo liên kết chia sẻ dự án công khai ở chế độ chỉ đọc (Read-only). Người dùng khác có thể **Fork (sao chép)** toàn bộ cấu trúc dự án về Workspace cá nhân chỉ với 1 click.
- ⏱️ **Lịch sử chạy code**: Lưu trữ và hiển thị 20 lần chạy gần nhất kèm trạng thái, output và thời gian thực thi chi tiết.

---

## 🛠️ Công Nghệ Sử Dụng (Tech Stack)

| Thành phần | Công nghệ / Thư viện | Vai trò |
| :--- | :--- | :--- |
| **Core Framework** | ASP.NET Core 8 (Razor Pages) | Xây dựng cấu trúc ứng dụng và giao diện Page Model. |
| **Database** | Supabase (PostgreSQL) | Lưu trữ thông tin người dùng, snippet, lịch sử chạy và quota. |
| **Authentication** | Supabase Auth (Email + Google OAuth) | Quản lý định danh và token phiên làm việc. |
| **ORM** | Entity Framework Core (EF Core) | Truy vấn dữ liệu từ database thông qua mô hình hướng đối tượng C#. |
| **Frontend Editor** | Monaco Editor CDN | Soạn thảo code trực tuyến. |
| **Execution Engine** | Local JDK 17 (Self-hosted) / Docker OpenJDK 17 | Biên dịch và chạy mã nguồn Java cục bộ / container. |

---

## 🚀 Hướng Dẫn Cài Đặt & Chạy Local

### 1. Yêu cầu hệ thống
- Cài đặt [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
- Cài đặt [Git](https://git-scm.com/).
- Có tài khoản và project hoạt động trên [Supabase](https://supabase.com/).

### 2. Thiết lập Database trên Supabase
Chạy các truy vấn SQL DDL sau trên **SQL Editor** của Supabase để tạo cấu trúc bảng và trigger đồng bộ profile:

```sql
-- 1. Bảng lưu trữ profile người dùng
CREATE TABLE public.users_profile (
    id uuid PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    full_name text,
    avatar_url text,
    updated_at timestamptz DEFAULT now()
);

-- 2. Bảng lưu trữ dự án Java (Snippets)
CREATE TABLE public.snippets (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    title text NOT NULL,
    code text NOT NULL, -- Lưu trữ mảng JSON cấu trúc file dự án
    share_id varchar(8) UNIQUE NULL,
    is_public boolean NOT NULL DEFAULT false,
    created_at timestamptz DEFAULT now(),
    updated_at timestamptz DEFAULT now()
);

-- 3. Bảng lịch sử chạy code
CREATE TABLE public.run_history (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    snippet_id bigint REFERENCES public.snippets(id) ON DELETE SET NULL,
    code text NOT NULL,
    output text,
    status text NOT NULL,
    duration_ms integer NOT NULL,
    created_at timestamptz DEFAULT now()
);

-- 4. Bảng giới hạn quota hàng ngày
CREATE TABLE public.run_quota (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    date date NOT NULL DEFAULT CURRENT_DATE,
    count integer NOT NULL DEFAULT 0,
    CONSTRAINT unique_user_date UNIQUE (user_id, date)
);

-- Bật RLS (Row Level Security) cho các bảng và thiết lập chính sách bảo mật
ALTER TABLE public.snippets ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.users_profile ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.run_history ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.run_quota ENABLE ROW LEVEL SECURITY;

-- Tạo trigger tự động đồng bộ profile khi user đăng ký qua Supabase Auth
CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS trigger AS $$
BEGIN
  INSERT INTO public.users_profile (id, full_name, avatar_url)
  VALUES (
    new.id,
    COALESCE(new.raw_user_meta_data->>'full_name', new.raw_user_meta_data->>'name', new.email),
    new.raw_user_meta_data->>'avatar_url'
  );
  RETURN new;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

CREATE TRIGGER on_auth_user_created
  AFTER INSERT ON auth.users
  FOR EACH ROW EXECUTE FUNCTION public.handle_new_user();
```

### 3. Cấu hình file `appsettings.Development.json`
Tạo hoặc cập nhật file `appsettings.Development.json` nằm tại thư mục gốc dự án:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=aws-1-ap-southeast-2.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.YOUR_PROJECT_REF;Password=YOUR_DATABASE_PASSWORD;SSL Mode=Require;Trust Server Certificate=true;"
  },
  "Supabase": {
    "Url": "https://YOUR_PROJECT_REF.supabase.co",
    "AnonKey": "YOUR_SUPABASE_ANON_KEY"
  }
}
```

*Lưu ý: Do các nhà mạng Việt Nam thỉnh thoảng gặp khó khăn trong việc định tuyến IPv6 trực tiếp đến Supabase, connection string trên sử dụng **Session Pooler (Port 5432)** để đảm bảo kết nối ổn định 100%.*

### 4. Khởi chạy dự án local
Mở terminal tại thư mục dự án và chạy các lệnh sau:

```bash
# Khôi phục dependencies
dotnet restore

# Biên dịch dự án
dotnet build

# Chạy ứng dụng
dotnet run --urls="http://localhost:5000"
```

Mở trình duyệt truy cập: `http://localhost:5000`

---

## 📖 Hướng Dẫn Sử Dụng IDE

### 1. Đăng ký & Đăng nhập
- Bấm vào góc trên bên phải để vào trang đăng nhập.
- Bạn có thể **đăng ký tài khoản mới** bằng Email/Mật khẩu hoặc sử dụng **Google OAuth** (nếu bạn đã cấu hình client_id trong Supabase dashboard).

### 2. Viết Code OOP & Quản lý nhiều tệp tin
- Sau khi đăng nhập, giao diện IDE sẽ hiển thị các file của dự án mẫu mặc định ở thanh bên trái Monaco Editor (Panel **Tệp tin dự án (OOP)**).
- Click vào từng file (`Main.java`, `animals/Animal.java`, ...) để chuyển tab và viết code.
- Để **tạo file mới**, bấm nút `+` trên thanh Explorer, nhập tên file Java (Ví dụ: `com/example/MyHelper.java`). Hệ thống sẽ tự động tạo package tương ứng cho bạn.
- Bấm nút **Chạy** để biên dịch toàn bộ dự án cùng lúc.

### 3. Lưu & Chia sẻ
- Nhập tiêu đề cho dự án tại ô nhập liệu toolbar phía trên.
- Chọn **Chế độ công khai** nếu bạn muốn chia sẻ code cho mọi người.
- Bấm nút **Lưu** hoặc **Cập nhật** để đồng bộ code lên Cloud Database.
- Bấm **Copy link chia sẻ** để gửi cho bạn bè. Khi bạn bè mở link, họ sẽ thấy giao diện chỉ đọc và có thể bấm nút **Fork về Workspace** để sao chép dự án về tài khoản của họ.

---

## 🚢 Hướng Dẫn Deploy lên Railway.app

Dự án đã được cấu hình sẵn `Dockerfile` multi-stage build tối ưu hóa và tệp cấu hình `railway.toml`.

Các bước thực hiện:
1. Đẩy dự án lên một Repo GitHub cá nhân của bạn.
2. Truy cập [Railway.app](https://railway.app/) và tạo một New Project liên kết với Github Repo đó.
3. Thêm các biến môi trường (Environment Variables) trong tab **Variables** trên Railway:
   - `ConnectionStrings__DefaultConnection`: Chuỗi kết nối Postgres của Supabase.
   - `Supabase__Url`: URL Supabase.
   - `Supabase__AnonKey`: Khóa Anonymous của Supabase.
4. Railway sẽ tự động nhận diện `Dockerfile`, build và deploy ứng dụng lên production.

---

## 📝 Bản quyền & Giấy phép
Dự án được phát triển phục vụ mục đích học tập và nghiên cứu công nghệ C# .NET Core & Supabase.
