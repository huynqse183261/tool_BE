
-- =====================================================
-- AI SOCIAL CONTENT PLATFORM DATABASE
-- PostgreSQL
-- ASP.NET CORE + AI + CLOUDINARY + SCHEDULER
-- =====================================================

-- =====================================================
-- DROP TABLES
-- =====================================================

DROP TABLE IF EXISTS ai_feedbacks CASCADE;
DROP TABLE IF EXISTS ai_image_analysis CASCADE;
DROP TABLE IF EXISTS prompt_examples CASCADE;
DROP TABLE IF EXISTS scheduled_jobs CASCADE;
DROP TABLE IF EXISTS post_platforms CASCADE;
DROP TABLE IF EXISTS ai_generations CASCADE;
DROP TABLE IF EXISTS post_images CASCADE;
DROP TABLE IF EXISTS posts CASCADE;
DROP TABLE IF EXISTS social_accounts CASCADE;
DROP TABLE IF EXISTS google_drive_connections CASCADE;
DROP TABLE IF EXISTS brand_settings CASCADE;
DROP TABLE IF EXISTS users CASCADE;

-- =====================================================
-- USERS
-- =====================================================

CREATE TABLE users (
    id SERIAL PRIMARY KEY,

    email VARCHAR(255) NOT NULL UNIQUE,

    password_hash TEXT NOT NULL,

    full_name VARCHAR(255),

    plan VARCHAR(50) DEFAULT 'free',

    is_active BOOLEAN DEFAULT TRUE,

    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),

    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- =====================================================
-- BRAND SETTINGS
-- =====================================================

CREATE TABLE brand_settings (
    id SERIAL PRIMARY KEY,

    user_id INT NOT NULL UNIQUE
        REFERENCES users(id)
        ON DELETE CASCADE,

    brand_name VARCHAR(255),

    tone_style VARCHAR(100),

    caption_length VARCHAR(50),

    emoji_level INT DEFAULT 1,

    cta_style VARCHAR(100),

    writing_language VARCHAR(50) DEFAULT 'vi',

    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- =====================================================
-- GOOGLE DRIVE CONNECTIONS
-- =====================================================

CREATE TABLE google_drive_connections (
    id SERIAL PRIMARY KEY,

    user_id INT NOT NULL
        REFERENCES users(id)
        ON DELETE CASCADE,

    google_email VARCHAR(255),

    access_token TEXT,

    refresh_token TEXT,

    token_expired_at TIMESTAMP WITH TIME ZONE,

    folder_id TEXT,

    is_active BOOLEAN DEFAULT TRUE,

    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- =====================================================
-- SOCIAL ACCOUNTS
-- =====================================================

CREATE TABLE social_accounts (
    id SERIAL PRIMARY KEY,

    user_id INT NOT NULL
        REFERENCES users(id)
        ON DELETE CASCADE,

    platform VARCHAR(50) NOT NULL,

    platform_user_id VARCHAR(255),

    page_id VARCHAR(255),

    access_token TEXT,

    refresh_token TEXT,

    token_expired_at TIMESTAMP WITH TIME ZONE,

    is_active BOOLEAN DEFAULT TRUE,

    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- =====================================================
-- POSTS
-- =====================================================

CREATE TABLE posts (
    id SERIAL PRIMARY KEY,

    user_id INT NOT NULL
        REFERENCES users(id)
        ON DELETE CASCADE,

    title VARCHAR(255),

    ai_title VARCHAR(255),

    caption TEXT,

    hashtags TEXT,

    content_type VARCHAR(100),

    scene_type VARCHAR(100),

    mood_type VARCHAR(100),

    status VARCHAR(50) NOT NULL DEFAULT 'Draft',

    scheduled_at TIMESTAMP WITH TIME ZONE,

    published_at TIMESTAMP WITH TIME ZONE,

    edited_by_user BOOLEAN DEFAULT FALSE,

    generation_version INT DEFAULT 1,

    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),

    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- =====================================================
-- POST IMAGES
-- =====================================================

CREATE TABLE post_images (
    id SERIAL PRIMARY KEY,

    post_id INT NOT NULL
        REFERENCES posts(id)
        ON DELETE CASCADE,

    image_url TEXT NOT NULL,

    google_drive_file_id TEXT,

    display_order INT DEFAULT 1,

    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- =====================================================
-- AI IMAGE ANALYSIS
-- =====================================================

CREATE TABLE ai_image_analysis (
    id SERIAL PRIMARY KEY,

    post_id INT NOT NULL
        REFERENCES posts(id)
        ON DELETE CASCADE,

    visual_summary TEXT,

    collector_context TEXT,

    detected_objects TEXT,

    content_type VARCHAR(100),

    scene_type VARCHAR(100),

    mood_type VARCHAR(100),

    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- =====================================================
-- AI GENERATIONS
-- =====================================================

CREATE TABLE ai_generations (
    id SERIAL PRIMARY KEY,

    post_id INT NOT NULL
        REFERENCES posts(id)
        ON DELETE CASCADE,

    provider VARCHAR(100),

    model VARCHAR(100),

    prompt TEXT,

    generated_caption TEXT,

    generated_title VARCHAR(255),

    generated_hashtags TEXT,

    vision_analysis TEXT,

    raw_response TEXT,

    examples_used TEXT,

    content_type VARCHAR(100),

    scene_type VARCHAR(100),

    mood_type VARCHAR(100),

    tokens_used INT DEFAULT 0,

    generation_time_ms INT DEFAULT 0,

    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- =====================================================
-- AI FEEDBACKS
-- =====================================================

CREATE TABLE ai_feedbacks (
    id SERIAL PRIMARY KEY,

    post_id INT NOT NULL
        REFERENCES posts(id)
        ON DELETE CASCADE,

    ai_generation_id INT
        REFERENCES ai_generations(id)
        ON DELETE SET NULL,

    user_rating INT,

    user_edited_caption TEXT,

    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- =====================================================
-- PROMPT EXAMPLES
-- =====================================================

CREATE TABLE prompt_examples (
    id SERIAL PRIMARY KEY,

    content_type VARCHAR(100),

    scene_type VARCHAR(100),

    mood_type VARCHAR(100),

    example_title VARCHAR(255),

    example_caption TEXT,

    priority_score INT DEFAULT 1,

    is_active BOOLEAN DEFAULT TRUE,

    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- =====================================================
-- POST PLATFORMS
-- =====================================================

CREATE TABLE post_platforms (
    id SERIAL PRIMARY KEY,

    post_id INT NOT NULL
        REFERENCES posts(id)
        ON DELETE CASCADE,

    platform VARCHAR(50) NOT NULL,

    social_account_id INT
        REFERENCES social_accounts(id)
        ON DELETE SET NULL,

    platform_post_id VARCHAR(255),

    status VARCHAR(50) DEFAULT 'Pending',

    error_message TEXT,

    published_at TIMESTAMP WITH TIME ZONE
);

-- =====================================================
-- SCHEDULED JOBS
-- =====================================================

CREATE TABLE scheduled_jobs (
    id SERIAL PRIMARY KEY,

    post_id INT NOT NULL
        REFERENCES posts(id)
        ON DELETE CASCADE,

    job_type VARCHAR(100) NOT NULL,

    execute_at TIMESTAMP WITH TIME ZONE NOT NULL,

    status VARCHAR(50) DEFAULT 'Pending',

    retry_count INT DEFAULT 0,

    last_error TEXT,

    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- =====================================================
-- INDEXES
-- =====================================================

CREATE INDEX idx_posts_user_status
ON posts(user_id, status);

CREATE INDEX idx_posts_content_type
ON posts(content_type);

CREATE INDEX idx_posts_scene_type
ON posts(scene_type);

CREATE INDEX idx_posts_scheduled_at
ON posts(scheduled_at);

CREATE INDEX idx_ai_generations_post
ON ai_generations(post_id);

CREATE INDEX idx_ai_analysis_post
ON ai_image_analysis(post_id);

CREATE INDEX idx_prompt_examples_lookup
ON prompt_examples(content_type, scene_type, mood_type);

CREATE INDEX idx_scheduled_jobs_status_execute
ON scheduled_jobs(status, execute_at);

CREATE INDEX idx_post_platforms_post_platform
ON post_platforms(post_id, platform);

CREATE INDEX idx_social_accounts_user_platform
ON social_accounts(user_id, platform);

CREATE INDEX idx_google_drive_connections_user
ON google_drive_connections(user_id);

-- =====================================================
-- SAMPLE PROMPT EXAMPLES
-- =====================================================

INSERT INTO prompt_examples (
    content_type,
    scene_type,
    mood_type,
    example_title,
    example_caption
)
VALUES
(
    'ProductShowcase',
    'IndustrialGarage',
    'Luxury',
    'INDUSTRIAL GARAGE',
    '🔥INDUSTRIAL GARAGE _ GRE911DIO-64 🔥

🏭 Một không gian garage mang phong cách industrial với nhiều khu vực trưng bày và setup khác nhau trong cùng một mô hình.

Từ những chiếc JDM quen thuộc, xe độ đường phố cho đến các mẫu xe hiệu năng cao, GRE911DIO-64 mang đến một background đậm chất workshop để hoàn thiện bộ sưu tập 1:64 của bạn.

📦 Mã sản phẩm: GRE911DIO-64
🏭 Tên sản phẩm: Industrial Garage
📏 Tỷ lệ: 1:64

📩 Inbox GRE•911 để nhận báo giá và thông tin hàng sẵn có.'
);

INSERT INTO prompt_examples (
    content_type,
    scene_type,
    mood_type,
    example_title,
    example_caption
)
VALUES
(
    'DioramaStorytelling',
    'JapaneseStreet',
    'Cinematic',
    'MỘT GÓC 7-ELEVEN TRONG ĐÊM',
    'Một chiếc Skyline đỏ.
Và vài khoảnh khắc rất đời thường ở tỉ lệ 1:64.

Không cần quá nhiều thứ để tạo nên cảm giác của một thành phố thu nhỏ.

1:64 — nhưng vẫn giữ được nhịp sống rất thật.

Making Things Small. Creating the Culture.'
);

INSERT INTO prompt_examples (
    content_type,
    scene_type,
    mood_type,
    example_title,
    example_caption
)
VALUES
(
    'EventCoverage',
    'EventBooth',
    'Community',
    'KHI ĐAM MÊ KHÔNG GIỚI HẠN',
    'Một ngày đầy cảm hứng cùng cộng đồng diecast và những bộ sưu tập đầy cá tính.

Không chỉ là mô hình.
Đây là văn hoá, là đam mê và là nơi mọi collector gặp nhau.'
);

ALTER TABLE users
ADD COLUMN fcm_token text;
ALTER TABLE posts
ADD COLUMN facebook_post_id text;